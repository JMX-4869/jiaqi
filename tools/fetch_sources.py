# -*- coding: utf-8 -*-
"""
假期查询 · 抓取脚本（更新管道的第一段）

做三件事：
  1. 按 data/holidays.json 里登记过的每个来源 URL，把原文抓下来
  2. 从正文里抽日期，按上下文归到「寒假开始 / 寒假结束 / 暑假开始 / 开学」
  3. 和库里现有日期逐条比对，输出差异报告

它不直接改库 —— 抓得对不对得人看一眼。确认后改 build_data.py 再跑它，才是闭环。

用法：
    python tools/fetch_sources.py                # 抓全部来源
    python tools/fetch_sources.py --only bj      # 只抓北京的源
    python tools/fetch_sources.py --timeout 20   # 单请求超时秒数

输出：data/fetch_report.json + 控制台摘要
退出码：0 = 全部对得上；2 = 有差异或缺页（值得人看一眼）
"""

import argparse
import gzip
import io
import json
import os
import re
import ssl
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DATA = os.path.join(ROOT, "data", "holidays.json")
REPORT = os.path.join(ROOT, "data", "fetch_report.json")

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/124.0 Safari/537.36")

# ---------------------------------------------------------------- 抓取

def http_get(url, timeout=15):
    """抓一个页面，返回 (状态码, 文本)。自己处理 gzip 和 GBK。"""
    req = urllib.request.Request(url, headers={
        "User-Agent": UA,
        "Accept": "text/html,application/xhtml+xml,*/*;q=0.8",
        "Accept-Language": "zh-CN,zh;q=0.9",
    })
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    with urllib.request.urlopen(req, timeout=timeout, context=ctx) as r:
        raw = r.read()
        code = r.getcode()
        ctype = r.headers.get("Content-Type", "")
        if r.headers.get("Content-Encoding", "").lower() == "gzip" or raw[:2] == b"\x1f\x8b":
            raw = gzip.decompress(raw)
    return code, decode(raw, ctype)


def decode(raw, ctype):
    """按 header charset → meta charset → utf-8 → gb18030 的顺序解码。"""
    cands = []
    m = re.search(r"charset=([\w-]+)", ctype or "", re.I)
    if m:
        cands.append(m.group(1))
    head = raw[:4000].decode("ascii", "ignore")
    m = re.search(r'charset=["\']?([\w-]+)', head, re.I)
    if m:
        cands.append(m.group(1))
    cands += ["utf-8", "gb18030"]

    seen = set()
    for enc in cands:
        enc = enc.lower()
        if enc in seen:
            continue
        seen.add(enc)
        try:
            return raw.decode(enc)
        except (UnicodeDecodeError, LookupError):
            continue
    return raw.decode("utf-8", "replace")


def to_text(html):
    """粗剥标签，保留换行和段落边界（正文抽日期够用）。"""
    s = re.sub(r"(?is)<(script|style|noscript)[^>]*>.*?</\1>", " ", html)
    s = re.sub(r"(?i)<br\s*/?>", "\n", s)
    s = re.sub(r"(?i)</(p|div|li|tr|h\d|td)>", "\n", s)
    s = re.sub(r"(?s)<[^>]+>", " ", s)
    s = (s.replace("&nbsp;", " ").replace("&amp;", "&")
          .replace("&lt;", "<").replace("&gt;", ">").replace("&#160;", " "))
    s = re.sub(r"[ \t\u3000]+", " ", s)
    s = re.sub(r"\n\s*\n+", "\n", s)
    return s


# ---------------------------------------------------------------- 抽日期

DATE_FULL = re.compile(r"(\d{4})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})\s*日")
DATE_MD = re.compile(r"(?<!\d)(\d{1,2})\s*月\s*(\d{1,2})\s*日")

START_WORDS = ("开始", "起放", "起开始", "放寒假", "放暑假", "放假", "起", "至")
END_WORDS = ("结束", "止", "开学", "返校", "报到", "上课")

# 法定节假日的说法也要认，否则国务院那页一条都抽不出来
LEGAL_KINDS = [
    ("元旦", "newyear"), ("春节", "spring"), ("清明", "qingming"),
    ("劳动节", "labor"), ("五一", "labor"), ("端午", "dragon"),
    ("中秋", "midautumn"), ("国庆", "national"),
]


def ctx_kind(ctx):
    """看上下文判断是哪一种假期。认不出就返回 None。"""
    if "寒假" in ctx or "冬季" in ctx:
        return "winter"
    if "暑假" in ctx or "夏季" in ctx:
        return "summer"
    for word, kind in LEGAL_KINDS:
        if word in ctx:
            return kind
    return None


def is_start(ctx):
    return any(w in ctx for w in ("放寒假", "放暑假", "放假", "开始", "起"))


def is_end(ctx):
    return any(w in ctx for w in ("结束", "止", "开学", "报到", "上课"))


def extract(text, default_year):
    """从正文里抽日期候选。返回 [{date, kind, role, ctx}]"""
    out = []
    for m in DATE_FULL.finditer(text):
        y, mo, d = int(m.group(1)), int(m.group(2)), int(m.group(3))
        a, b = max(0, m.start() - 70), min(len(text), m.end() + 70)
        ctx = text[a:b].replace("\n", " ")
        try:
            iso = datetime(y, mo, d).strftime("%Y-%m-%d")
        except ValueError:
            continue
        out.append({
            "date": iso, "raw": m.group(0), "ctx": ctx,
            "kind": ctx_kind(ctx),
            "role": "start" if is_start(ctx) else ("end" if is_end(ctx) else "other"),
        })

    # 只有「1月23日」没有年份的，按默认学年补。
    # kind 认不出的也留着 —— 用来回答「这一页到底提没提这个日期」。
    for m in DATE_MD.finditer(text):
        mo, d = int(m.group(1)), int(m.group(2))
        a, b = max(0, m.start() - 70), min(len(text), m.end() + 70)
        ctx = text[a:b].replace("\n", " ")
        try:
            iso = datetime(default_year, mo, d).strftime("%Y-%m-%d")
        except ValueError:
            continue
        out.append({
            "date": iso, "raw": m.group(0), "ctx": ctx, "kind": ctx_kind(ctx),
            "role": "start" if is_start(ctx) else ("end" if is_end(ctx) else "other"),
        })

    uniq, seen = [], set()
    for c in out:
        key = (c["date"], c["kind"], c["role"])
        if key in seen:
            continue
        seen.add(key)
        uniq.append(c)
    return uniq


# ---------------------------------------------------------------- 比对

def collect_expectations(data):
    """从库里抽出「某个 URL 应该能印证哪些日期」。"""
    exp = {}

    def add(url, meta, holiday):
        if not url:
            return
        exp.setdefault(url, {"title": meta.get("title", ""), "items": []})
        exp[url]["items"].append({
            "where": meta["where"],
            "label": holiday["label"],
            "start": holiday["start"],
            "end": holiday.get("end", ""),
            "endDerived": holiday.get("endDerived", False),
        })

    for r in data.get("regions", []):
        for c in r["cities"]:
            for sk, p in c["policies"].items():
                s = p.get("source") or {}
                for h in p["holidays"]:
                    add(s.get("url"), {
                        "title": s.get("title", ""),
                        "where": f'{r["name"]} {c["name"]} {sk}',
                    }, h)

    nh = data.get("nationalHolidays") or {}
    ns = nh.get("source") or {}
    for h in nh.get("items", []):
        add(ns.get("url"), {"title": ns.get("title", ""), "where": "全国"}, h)

    for u in data.get("universities", []):
        s = u.get("source") or {}
        for h in u["holidays"]:
            add(s.get("url"), {"title": s.get("title", ""), "where": u["name"]}, h)

    return exp


def check(url, text, items):
    """页面里找到的日期 vs 库里记的日期。"""
    cands = extract(text, 2027)
    got = {c["date"] for c in cands}
    got_start = {c["date"] for c in cands if c["role"] == "start"}
    got_end = {c["date"] for c in cands if c["role"] == "end"}

    results = []
    for it in items:
        st = it["start"]
        en = it.get("end", "")
        derived = it.get("endDerived", False)
        results.append({
            "where": it["where"],
            "label": it["label"],
            "start": st,
            "startSeen": st in got,
            "startSeenAsStart": st in got_start,
            "end": en,
            # 推算出来的结束日期本来就不在原文里，不比对
            "endSeen": (en in got) if (en and not derived) else None,
            "endSeenAsEnd": (en in got_end) if (en and not derived) else None,
            "endDerived": derived,
        })
    return cands, results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="", help="只抓标题/URL 里含这个词的源")
    ap.add_argument("--timeout", type=float, default=15)
    ap.add_argument("--keep", type=int, default=3, help="报告里每个源保留几条候选")
    args = ap.parse_args()

    with open(DATA, encoding="utf-8") as f:
        data = json.load(f)

    exp = collect_expectations(data)
    if args.only:
        exp = {u: v for u, v in exp.items() if args.only in u or args.only in v["title"]}

    print(f"来源 {len(exp)} 个，开始抓…\n")

    report = {
        "fetchedAt": datetime.now(timezone(timedelta(hours=8))).isoformat(timespec="seconds"),
        "dataUpdatedAt": data.get("updatedAt", ""),
        "sources": [],
        "summary": {"sources": 0, "ok": 0, "failed": 0,
                    "items": 0, "startMissing": 0, "endMissing": 0},
        "diff": [],
    }

    for url, meta in exp.items():
        entry = {"url": url, "title": meta["title"], "ok": False,
                 "status": 0, "failedReason": "", "candidates": [], "items": []}
        t0 = time.time()
        try:
            code, text = http_get(url, args.timeout)
            entry["status"] = code
            if code // 100 != 2:
                raise RuntimeError(f"HTTP {code}")
            body = to_text(text)
            entry["chars"] = len(body)
            cands, results = check(url, body, meta["items"])
            entry["ok"] = True
            entry["candidates"] = cands[: args.keep]
            entry["items"] = results

            miss_s = [r for r in results if not r["startSeen"]]
            miss_e = [r for r in results if r["endSeen"] is False]
            report["summary"]["ok"] += 1
            report["summary"]["items"] += len(results)
            report["summary"]["startMissing"] += len(miss_s)
            report["summary"]["endMissing"] += len(miss_e)

            flag = "OK " if not miss_s and not miss_e else "差 "
            print(f"[{flag}] {meta['title'][:26]}")
            print(f"       {len(results)} 条 · 找到 {len(results) - len(miss_s)} 个开始日"
                  f" · {len(cands)} 个候选日期 · {entry['chars']} 字 · {time.time()-t0:.1f}s")
            for r in miss_s[:4]:
                print(f"       ! 库里 {r['where']} {r['label']} {r['start']} 页面没找到")
                report["diff"].append({**r, "url": url, "type": "start_missing"})
            for r in miss_e[:3]:
                print(f"       ~ {r['where']} {r['label']} 结束 {r['end']} 页面没确认")
                report["diff"].append({**r, "url": url, "type": "end_missing"})
        except Exception as e:
            entry["failedReason"] = f"{type(e).__name__}: {e}"
            report["summary"]["failed"] += 1
            print(f"[失败] {meta['title'][:26]} → {entry['failedReason']}")

        report["sources"].append(entry)

    report["summary"]["sources"] = len(report["sources"])

    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    with open(REPORT, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1)

    s = report["summary"]
    print(f"\n合计：来源 {s['sources']}（成功 {s['ok']} / 失败 {s['failed']}）"
          f" · 核对 {s['items']} 条 · 开始日缺失 {s['startMissing']} · 结束日未确认 {s['endMissing']}")
    print(f"报告 → {REPORT}")

    return 0 if s["failed"] == 0 and s["startMissing"] == 0 else 2


if __name__ == "__main__":
    sys.exit(main())
