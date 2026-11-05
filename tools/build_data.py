# -*- coding: utf-8 -*-
"""
假期查询 · 数据构建脚本

把下面登记的原始条目，生成 app 使用的 data/holidays.json。

设计原则（很重要，别改坏）：
1. 只收录能从公开来源读到的日期。读不到的宁可不写，UI 会显示「未收录」。
2. 每条数据必须带 source：标题、URL、发布日期、可信等级(grade)。
   grade = official  官方文件（教育厅/政府网站原文）
   grade = media     媒体转载官方校历（内容来自官方，但不是原文）
   grade = aggregate 第三方汇总（仅作参考，可能过期）
3. 结束日期若来源没写，用「下一个学期开学前一天」推算，并标 endDerived=true。
   连开学日期也没有的，就不写 end，UI 会显示「结束日期待公布」。
4. 大学：高校校历由各校自行制定，按学校逐条收录，不做省级统一数据。

运行：python tools/build_data.py
"""

import json
import os
import sys
from datetime import date, datetime, timedelta, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "data", "holidays.json")

ACADEMIC_YEAR = "2026-2027"
# 每次构建都刷新，三端靠它判断远端数据是不是比本地新
UPDATED_AT = datetime.now(timezone(timedelta(hours=8)))
# 这批原始条目最后一次人工核对原文的时刻（不是脚本运行时刻）
FETCHED_AT = "2026-11-05T22:30:00+08:00"

# 全国 31 个省级行政区（不含港澳台）。用来算「未收录」缺口，
# 让 App 能明确显示哪些地方没有数据，而不是从列表里消失。
ALL_PROVINCES = [
    ("110000", "北京市"), ("120000", "天津市"), ("130000", "河北省"),
    ("140000", "山西省"), ("150000", "内蒙古自治区"), ("210000", "辽宁省"),
    ("220000", "吉林省"), ("230000", "黑龙江省"), ("310000", "上海市"),
    ("320000", "江苏省"), ("330000", "浙江省"), ("340000", "安徽省"),
    ("350000", "福建省"), ("360000", "江西省"), ("370000", "山东省"),
    ("410000", "河南省"), ("420000", "湖北省"), ("430000", "湖南省"),
    ("440000", "广东省"), ("450000", "广西壮族自治区"), ("460000", "海南省"),
    ("500000", "重庆市"), ("510000", "四川省"), ("520000", "贵州省"),
    ("530000", "云南省"), ("540000", "西藏自治区"), ("610000", "陕西省"),
    ("620000", "甘肃省"), ("630000", "青海省"), ("640000", "宁夏回族自治区"),
    ("650000", "新疆维吾尔自治区"),
]


def d(s):
    return date.fromisoformat(s)


def span(label, htype, start, end=None, end_derived=True, note=""):
    """构造一条假期。暑假一律补 8/31（全国普遍 9/1 开学），标 endDerived。

    兼容写法：有人把 note 传在第 5 个位置（end_derived 的位置），
    这里认出来换回去，免得 endDerived 变成一段中文。
    """
    if isinstance(end_derived, str):
        if isinstance(note, bool):
            note, end_derived = end_derived, note
        else:
            note, end_derived = end_derived, True
    if end is None and htype == "summer":
        end = "2027-08-31"
        end_derived = True
    item = {"type": htype, "label": label, "start": start}
    if end:
        item["end"] = end
        item["endDerived"] = end_derived
    if note:
        item["note"] = note
    return item


def policy(scope, holidays, src, extra=None):
    p = {"scope": scope, "holidays": holidays, "source": src}
    if extra:
        p.update(extra)
    return p


# ---------------------------------------------------------------- 来源登记
S = {
    "bj": {
        "title": "北京市教育委员会关于印发2026—2027学年度各级各类学校校历的通知",
        "url": "https://jw.beijing.gov.cn/xxgk/2024zcwj/2024qtwj/202604/t20260417_4588623.html",
        "docNo": "京教办〔2026〕4号",
        "published": "2026-04-17",
        "grade": "official",
        "publisher": "北京市教育委员会",
    },
    "sh": {
        "title": "上海市中小学2026学年校历（静安区转发解读）",
        "url": "https://www.shanghai.gov.cn/nw17239/20260901/336cfe2355aa4d6ca3315852100674bc.html",
        "published": "2026-09-01",
        "grade": "media",
        "publisher": "上海市静安区人民政府（引上海市教委校历）",
    },
    "cq": {
        "title": "重庆中小学寒假时间定了！",
        "url": "https://www.cq.gov.cn/ywdt/bmts/202610/t20261013_16225627.html",
        "published": "2026-10-13",
        "grade": "media",
        "publisher": "重庆市人民政府网",
    },
    "gd": {
        "title": "广东中小学新学年开学、寒假、暑假时间定了（来源：广东教育传媒）",
        "url": "https://m.sohu.com/a/1066151298_132336/",
        "published": "2026-08-22",
        "grade": "media",
        "publisher": "新快报转载广东教育传媒",
    },
    "sc": {
        "title": "四川各地中小学寒假放假时间（据各市州教育局官微）",
        "url": "http://news.shaocn.com/guonei/2026/1019/113658.html",
        "published": "2026-10-19",
        "grade": "aggregate",
        "publisher": "中韶网汇总各地教育局发布",
    },
    "js": {
        "title": "江苏中小学开学时间确定（引江苏省教育厅通知及南京、苏州校历）",
        "url": "https://news.qq.com/rain/a/20260823A085YC00",
        "published": "2026-08-23",
        "grade": "media",
        "publisher": "江苏警方",
    },
    "nat": {
        "title": "国务院办公厅关于2027年部分节假日安排的通知",
        "url": "https://www.gov.cn/zhengce/content/202611/content_7085101.htm",
        "docNo": "国办发明电〔2026〕10号",
        "published": "2026-11-05",
        "grade": "official",
        "publisher": "国务院办公厅",
    },
    "sum1": {
        "title": "放假通知！全国多地中小学2027寒假时间确定了！",
        "url": "https://m.sohu.com/a/1097240142_100934",
        "published": "2026-11-04",
        "grade": "aggregate",
        "publisher": "校长传媒（汇总各地校历）",
    },
    "sum2": {
        "title": "2027中小学寒假时间出炉！多地寒假变长？",
        "url": "https://www.sohu.com/a/1096671272_120653457",
        "published": "2026-11-03",
        "grade": "aggregate",
        "publisher": "中考升学直通车（汇总各地校历）",
    },
    "wh": {
        "title": "芜湖市中小学寒假时间定了！（据芜湖市教育局教学周次表）",
        "url": "https://news.qq.com/rain/a/20261105A09E6C00",
        "published": "2026-11-05",
        "grade": "media",
        "publisher": "大皖新闻转载芜湖发布",
    },
    "lz": {
        "title": "兰州多所高校的寒假时间（整理自各高校校历）",
        "url": "https://finance.sina.com.cn/wm/2026-10-18/doc-inivrkxt6635956.shtml",
        "published": "2026-10-18",
        "grade": "media",
        "publisher": "兰州发布",
    },
    "gz": {
        "title": "贵州大中小学寒假放假时间→",
        "url": "http://news.sohu.com/a/1088144710_120437913",
        "published": "2026-10-15",
        "grade": "aggregate",
        "publisher": "知知贵阳（据云上毕节、贵阳本地宝、贵州各高校官网）",
    },
    "nm": {
        "title": "包头市中小学寒假时间公布（市教育局答复）",
        "url": "https://iapp.baotounews.com.cn/share/YS02ODktNDUzMjk5ODU.html",
        "published": "2026-10-20",
        "grade": "media",
        "publisher": "i包头 · 包头市网络群众工作中心 / 市教育局",
    },
    "gg": {
        "title": "贵港市教育局关于印发全市中小学2026-2027学年度校历的通知",
        "url": "http://gxgg.wenming.cn/wmxy/202607/t20260702_9287421.html",
        "published": "2026-07-02",
        "grade": "official",
        "publisher": "贵港市教育局",
    },
    "yl": {
        "title": "玉林市2026—2027学年校历出炉",
        "url": "https://www.gxylnews.com/xwk/jy/197001/t19700121_953578.html",
        "published": "2026-06-26",
        "grade": "media",
        "publisher": "玉林日报（引玉林市教育局校历）",
    },
    "yn": {
        "title": "放假通知，2027中小学寒假放假时间确定了",
        "url": "https://www.163.com/dy/article/L7N7310P0536E91Z_pdya11y.html",
        "published": "2026-09-25",
        "grade": "aggregate",
        "publisher": "凯旋学长（汇总各地教育部门校历）",
    },
    "jl": {
        "title": "吉林省多所学校公布寒假时间",
        "url": "https://m.thepaper.cn/newsDetail_forward_34252002",
        "published": "2026-10-14",
        "grade": "media",
        "publisher": "吉林日报（掌上长春综合）",
    },
    "zj_qj": {
        "title": "2026年浙江各地中小学秋假时间来了",
        "url": "https://www.lanews.com.cn/content/2026-09/04/content_9927109.html",
        "published": "2026-09-04",
        "grade": "media",
        "publisher": "临安新闻网（汇总浙江各地教育局公布）",
    },
    "sd_qj": {
        "title": "连休5天，山东又1市确定秋假时间；此前已有11市公布",
        "url": "https://m.gmw.cn/toutiao/2026-09/08/content_1304560932.htm",
        "published": "2026-09-08",
        "grade": "media",
        "publisher": "光明网转齐鲁晚报（引各市教育局发布）",
    },
}


def bj():
    comp = [
        span("寒假", "winter", "2027-01-18", "2027-02-21", False),
        span("暑假", "summer", "2027-07-06", "2027-08-31", False),
    ]
    non = [
        span("寒假", "winter", "2027-01-25", "2027-02-21", False),
        span("暑假", "summer", "2027-07-13", "2027-08-31", False),
    ]
    return {
        "code": "110000", "name": "北京市", "abbr": "北京", "kind": "municipality",
        "cities": [{
            "code": "110100", "name": "北京市",
            "policies": {
                "primary": policy("义务教育阶段（小学）", comp, S["bj"]),
                "junior": policy("义务教育阶段（初中）", comp, S["bj"]),
                "senior": policy("非义务教育阶段（高中/中职）", non, S["bj"]),
            },
        }],
    }


def sh():
    hols = [
        span("寒假", "winter", "2027-01-23", "2027-02-21", False),
        span("暑假", "summer", "2027-07-01", "2027-08-31", False),
    ]
    return {
        "code": "310000", "name": "上海市", "abbr": "上海", "kind": "municipality",
        "cities": [{
            "code": "310100", "name": "上海市",
            "policies": {
                "primary": policy("小学", hols, S["sh"]),
                "junior": policy("初中", hols, S["sh"]),
                "senior": policy("高中", hols, S["sh"], {"note": "全市中小学统一按市教委校历执行"}),
            },
        }],
    }


def cq():
    comp = [
        span("寒假", "winter", "2027-01-19", "2027-02-21", False),
        span("暑假", "summer", "2027-07-03", None),
    ]
    senior = [
        span("寒假", "winter", "2027-01-26", "2027-02-21", False),
        span("暑假", "summer", "2027-07-03", None),
    ]
    return {
        "code": "500000", "name": "重庆市", "abbr": "重庆", "kind": "municipality",
        "cities": [{
            "code": "500100", "name": "重庆市",
            "policies": {
                "primary": policy("小学", comp, S["cq"], {"note": "幼儿园可参照执行，市里不统一"}),
                "junior": policy("初中", comp, S["cq"]),
                "senior": policy("高中（含中职）", senior, S["cq"]),
            },
        }],
    }


# 广东：义务教育(小学/初中) / 普通高中，各市寒假、暑假起始
GD = [
    ("广州市", "440100", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("深圳市", "440300", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("珠海市", "440400", "2027-01-25", "2027-07-12", "2027-02-01", "2027-07-19"),
    ("汕头市", "440500", "2027-01-24", "2027-07-11", "2027-01-31", "2027-07-18"),
    ("佛山市", "440600", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("韶关市", "440200", "2027-01-24", "2027-07-11", "2027-01-31", "2027-07-18"),
    ("河源市", "441600", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("梅州市", "441400", "2027-01-24", "2027-07-11", "2027-01-31", "2027-07-18"),
    ("惠州市", "441300", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("汕尾市", "441500", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("东莞市", "441900", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("中山市", "442000", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("江门市", "440700", "2027-01-24", "2027-07-11", "2027-01-31", "2027-07-18"),
    ("阳江市", "441700", "2027-01-24", "2027-07-11", "2027-01-31", "2027-07-18"),
    ("湛江市", "440800", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-16"),
    ("茂名市", "440900", "2027-01-23", "2027-07-10", "2027-01-23", "2027-07-10"),
    ("肇庆市", "441200", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("清远市", "441800", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("潮州市", "445100", "2027-01-24", "2027-07-11", "2027-01-31", "2027-07-18"),
    ("揭阳市", "445200", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
    ("云浮市", "445300", "2027-01-23", "2027-07-10", "2027-01-30", "2027-07-17"),
]


def gd():
    cities = []
    for name, code, c_w, c_s, h_w, h_s in GD:
        # 广东各市校历只写了「寒假开始」和「春季开学 2/22」，
        # 2/21 这个结束日是倒推的，如实标 endDerived=True。
        comp = [span("寒假", "winter", c_w, "2027-02-21", True,
                     "结束日期按全省春季开学 2/22 前一天倒推"),
                span("暑假", "summer", c_s)]
        sen = [span("寒假", "winter", h_w, "2027-02-21", True,
                    "结束日期按全省春季开学 2/22 前一天倒推"),
               span("暑假", "summer", h_s)]
        cities.append({
            "code": code, "name": name,
            "policies": {
                "primary": policy("义务教育阶段（小学）", comp, S["gd"]),
                "junior": policy("义务教育阶段（初中）", comp, S["gd"]),
                "senior": policy("普通高中", sen, S["gd"]),
            },
        })
    return {
        "code": "440000", "name": "广东省", "abbr": "广东", "kind": "province",
        "cities": cities,
        "common": {"springStart": "2027-02-22", "autumnStart": "2026-09-01"},
    }


# 四川：(市州, code, 义寒假起, 高寒假起, 备注)
SC = [
    ("成都市", "510100", "2027-01-23", "2027-01-23", "幼儿园 1/23 起"),
    ("自贡市", "510300", "2027-01-16", "2027-01-23", "小学/幼儿园 1/16，中学 1/23"),
    ("攀枝花市", "510400", "2027-01-23", "2027-01-23", "全县中小学统一"),
    ("泸州市", "510500", "2027-01-26", "2027-01-26", "全市中小学统一"),
    ("乐山市", "511100", "2027-01-23", "2027-01-30", "幼儿园参照义务教育"),
    ("南充市", "511300", "2027-01-23", "2027-01-23", "幼儿园参照义务教育"),
    ("德阳市", "510600", "2027-01-23", "2027-01-23", "幼儿园参照义务教育"),
    ("内江市", "511000", "2027-01-23", "2027-01-23", ""),
    ("眉山市", "511400", "2027-01-23", "2027-01-23", ""),
    ("广安市", "511600", "2027-01-23", "2027-01-23", ""),
    ("雅安市", "511800", "2027-01-23", "2027-01-23", ""),
    ("凉山州", "513400", "2027-01-25", "2027-01-25", "义务教育与高中同日"),
]


def sc():
    cities = []
    for name, code, c_w, h_w, note in SC:
        comp = [span("寒假", "winter", c_w, "2027-02-21",
                     note or "结束日期按春季开学前一天推算")]
        sen = [span("寒假", "winter", h_w, "2027-02-21",
                    note or "结束日期按春季开学前一天推算")]
        cities.append({
            "code": code, "name": name,
            "policies": {
                "primary": policy("小学（及幼儿园）", comp, S["sc"]),
                "junior": policy("初中", comp, S["sc"]),
                "senior": policy("普通高中", sen, S["sc"]),
            },
        })
    return {
        "code": "510000", "name": "四川省", "abbr": "四川", "kind": "province",
        "cities": cities,
        "common": {"springStart": "2027-02-22"},
    }


def js():
    hols = [span("寒假", "winter", "2027-01-27", "2027-02-21",
                 "来源仅覆盖南京、苏州两市校历；结束日期按春季开学前一天推算")]
    pol = {
        "primary": policy("小学", hols, S["js"], {"note": "部分城市数据"}),
        "junior": policy("初中", hols, S["js"], {"note": "部分城市数据"}),
        "senior": policy("高中", hols, S["js"], {"note": "部分城市数据"}),
    }
    return {
        "code": "320000", "name": "江苏省", "abbr": "江苏", "kind": "province",
        "cities": [
            {"code": "320100", "name": "南京市", "policies": pol},
            {"code": "320500", "name": "苏州市", "policies": pol},
        ],
        "common": {"springStart": "2027-02-27", "note": "南京、苏州校历于 2027-01-26 结束学期"},
    }


# ------------------------------------------------- 仅拿到寒假起始日的地区
# (省名, 省code, 类型, [(市名, 市code, 义寒假起或None, 高寒假起或None, 备注)], 来源)
def _simple(prov_name, prov_code, cities_def, src, kind="province"):
    cities = []
    for cname, ccode, c_w, h_w, note in cities_def:
        pol = {}
        if c_w:
            hols = [span("寒假", "winter", c_w, None, True,
                         note or "结束日期以当地教育局通知为准")]
            pol["primary"] = policy("小学", hols, src)
            pol["junior"] = policy("初中", hols, src)
        if h_w:
            hols = [span("寒假", "winter", h_w, None, True,
                         note or "结束日期以当地教育局通知为准")]
            pol["senior"] = policy("高中", hols, src)
        cities.append({"code": ccode, "name": cname, "policies": pol})
    return {
        "code": prov_code, "name": prov_name, "abbr": prov_name.replace("省", "").replace("市", ""),
        "kind": kind, "cities": cities,
    }


def extras():
    out = []

    # 天津（直辖市）
    out.append(_simple("天津市", "120000", [
        ("天津市", "120100", "2027-01-19", "2027-01-26", "")
    ], S["sum1"], "municipality"))

    # 辽宁
    out.append(_simple("辽宁省", "210000", [
        ("沈阳市", "210100", "2027-01-15", "2027-01-22", "")
    ], S["sum1"]))

    # 浙江
    out.append(_simple("浙江省", "330000", [
        ("杭州市", "330100", "2027-01-30", "2027-01-30", "杭州市中小学统一 1/30 放假（全国最晚之一）")
    ], S["sum1"]))

    # 山东
    out.append(_simple("山东省", "370000", [
        ("济南市", "370100", "2027-01-23", "2027-01-30", "")
    ], S["sum1"]))

    # 江西（全省口径）
    out.append(_simple("江西省", "360000", [
        ("全省", "360000", "2027-01-23", "2027-01-30", "省级汇总口径")
    ], S["sum1"]))

    # 福建
    out.append(_simple("福建省", "350000", [
        ("福州市", "350100", "2027-01-28", "2027-01-30", "高中、中职 1/30 起"),
        ("厦门市", "350200", "2027-01-28", "2027-01-30", ""),
    ], S["sum1"]))

    # 湖北
    out.append(_simple("湖北省", "420000", [
        ("武汉市", "420100", "2027-01-23", "2027-01-30", "中小学、幼儿园 1/23 起")
    ], S["sum1"]))

    # 湖南
    out.append(_simple("湖南省", "430000", [
        ("长沙市", "430100", "2027-01-24", "2027-01-24", "长沙未分学段单列")
    ], S["sum2"]))

    # 海南
    out.append(_simple("海南省", "460000", [
        ("全省", "460000", "2027-01-16", "2027-01-23", "省级汇总口径")
    ], S["sum1"]))

    # 陕西
    out.append(_simple("陕西省", "610000", [
        ("西安市", "610100", "2027-01-22", "2027-01-29", "")
    ], S["sum1"]))

    # 安徽（芜湖，官方教育局周次表）
    out.append(_simple("安徽省", "340000", [
        ("芜湖市", "340200", "2027-01-27", "2027-01-30", "高三年级 2027-02-15 上课")
    ], S["wh"]))

    return out


def national():
    items = [
        {"type": "newyear", "label": "元旦", "start": "2027-01-01", "end": "2027-01-03", "days": 3},
        {"type": "spring", "label": "春节", "start": "2027-02-05", "end": "2027-02-13", "days": 9,
         "note": "1月31日、2月14日上班"},
        {"type": "qingming", "label": "清明节", "start": "2027-04-03", "end": "2027-04-05", "days": 3},
        {"type": "labor", "label": "劳动节", "start": "2027-05-01", "end": "2027-05-05", "days": 5,
         "note": "5月8日上班"},
        {"type": "dragon", "label": "端午节", "start": "2027-06-09", "end": "2027-06-09", "days": 1},
        {"type": "midautumn", "label": "中秋节", "start": "2027-09-15", "end": "2027-09-15", "days": 1},
        {"type": "national", "label": "国庆节", "start": "2027-10-01", "end": "2027-10-07", "days": 7,
         "note": "9月26日、10月9日上班"},
        {"type": "children", "label": "儿童节", "start": "2027-06-01", "end": "2027-06-01", "days": 1,
         "note": "不满 14 周岁的少年儿童放假 1 天，属校历惯例，不在国务院节假日通知里"},
    ]
    return {"year": 2027, "items": items, "source": S["nat"]}


PROV_FULL = {
    "北京": "北京市", "天津": "天津市", "上海": "上海市", "重庆": "重庆市",
    "河北": "河北省", "山西": "山西省", "辽宁": "辽宁省", "吉林": "吉林省",
    "黑龙江": "黑龙江省", "江苏": "江苏省", "浙江": "浙江省", "安徽": "安徽省",
    "福建": "福建省", "江西": "江西省", "山东": "山东省", "河南": "河南省",
    "湖北": "湖北省", "湖南": "湖南省", "广东": "广东省", "海南": "海南省",
    "四川": "四川省", "贵州": "贵州省", "云南": "云南省", "陕西": "陕西省",
    "甘肃": "甘肃省", "青海": "青海省",
    "内蒙古": "内蒙古自治区", "广西": "广西壮族自治区", "西藏": "西藏自治区",
    "宁夏": "宁夏回族自治区", "新疆": "新疆维吾尔自治区",
}


def universities():
    """全国高校名单（教育部口径 3309 所）+ 已找到的校历。

    校历只有个别学校有 —— 高校校历各校自定，没有全国统一来源。
    没校历的学校照样列出来，点进去给教务处查询入口。
    """
    detail = {}

    def put(name, uid, prov, city, hols, note="", src=None):
        detail[name] = {"id": uid, "name": name, "province": prov, "city": city,
                        "holidays": hols, "note": note, "source": src or S["lz"]}

    # 甘肃兰州 7 所
    put("兰州大学", "lzu", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-18", "2027-02-17", "1/17 结束学期，2/18-19 注册，2/22 上课")])
    put("西北师范大学", "nwnu", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-16", "2027-02-26", "2/27-28 注册，3/1 上课")])
    put("兰州理工大学", "lut", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-18", "2027-02-26", "1/18 起放寒假")])
    put("兰州交通大学", "lzjtu", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-18", "2027-02-28", "校历明确 1/18—2/28 放寒假 6 周", False)])
    put("甘肃政法大学", "gsli", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-16", "2027-02-26", "校历明确 1/16—2/26，共 6 周", False)])
    put("兰州文理学院", "lzcu", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-18", "2027-02-26", "2/27-28 注册")])
    put("兰州工业学院", "lzit", "甘肃省", "兰州市",
        [span("寒假", "winter", "2027-01-11", "2027-02-26", "1/11 起放寒假")])

    # 吉林 7 所
    put("吉林大学", "jlu", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-16", "2027-02-28", "校历明确 1/16—2/28", False)], src=S["jl"])
    put("长春师范大学", "ccnu", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-18", None, True, "1/18 起放假")], src=S["jl"])
    put("长春工程学院", "ccit", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-09", None, True, "1/9 起放假")], src=S["jl"])
    put("长春医学高等专科学校", "ccmc", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-16", None, True, "1/16 起放假")], src=S["jl"])
    put("长春职业技术大学", "ccpu", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-11", "2027-02-28", True, "3/1 开学")], src=S["jl"])
    put("长春工业大学", "ccut", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-11", "2027-02-28", "校历明确 1/11—2/28", False)], src=S["jl"])
    put("延边大学", "ybu", "吉林省", "长春市",
        [span("寒假", "winter", "2027-01-04", "2027-02-28", "校历明确 1/4—2/28", False)], src=S["jl"])

    raw_path = os.path.join(ROOT, "data", "univ_raw.json")
    out = []
    if os.path.exists(raw_path):
        raw = json.load(open(raw_path, encoding="utf-8"))
        idx = 0
        for prov, v in raw.items():
            lst = v.get("all", []) if isinstance(v, dict) else v
            full = PROV_FULL.get(prov, prov)
            for x in lst:
                name = (x.get("name") or "").strip()
                if not name:
                    continue
                idx += 1
                d = detail.pop(name, None)
                item = {
                    "id": d["id"] if d else "u%04d" % idx,
                    "name": name,
                    "province": full,
                    "city": prov,
                    "tags": x.get("tags", []),
                    "holidays": d["holidays"] if d else [],
                }
                if d:
                    item["note"] = d["note"]
                    item["source"] = d["source"]
                out.append(item)

    # 名单里没有、但我们有数据的（别丢）
    for name, d in detail.items():
        out.append(d)
    return out


STAGE_META = [
    {"key": "primary", "label": "小学"},
    {"key": "junior", "label": "初中"},
    {"key": "senior", "label": "高中"},
    {"key": "university", "label": "大学"},
]


def gz():
    """贵州：小学 1/23，初中初一初二 1/23、初三 1/30，高中 1/30。"""
    comp = [span("寒假", "winter", "2027-01-23", None, True,
                 "初三单独推迟到 2027-01-30；结束日期以当地教育局通知为准")]
    sen = [span("寒假", "winter", "2027-01-30", None, True,
                "高一/高二/高三同为 1/30；结束日期以当地教育局通知为准")]
    pol = {
        "primary": policy("小学 1-6 年级（幼儿园可参照并提前一周）", comp, S["gz"]),
        "junior": policy("初中（初一初二 1/23，初三 1/30）", comp, S["gz"]),
        "senior": policy("普通高中（含中职参照）", sen, S["gz"]),
    }
    return {
        "code": "520000", "name": "贵州省", "abbr": "贵州", "kind": "province",
        "cities": [{"code": "520100", "name": "全省", "policies": pol}],
        "common": {"springStart": "2027-02-22", "note": "省级口径，各市州可在范围内微调"},
    }


def nm():
    """内蒙古 · 包头：义教 1/16-2/28，高中/中职 1/23-2/28。"""
    comp = [span("寒假", "winter", "2027-01-16", "2027-02-28", False, "共 6 周零 1 天")]
    sen = [span("寒假", "winter", "2027-01-23", "2027-02-28", False, "共 5 周零 1 天")]
    pol = {
        "primary": policy("义务教育阶段（小学）", comp, S["nm"]),
        "junior": policy("义务教育阶段（初中）", comp, S["nm"]),
        "senior": policy("普通高中、中等职业学校", sen, S["nm"]),
    }
    return {
        "code": "150000", "name": "内蒙古自治区", "abbr": "内蒙古", "kind": "province",
        "cities": [{"code": "150200", "name": "包头市", "policies": pol}],
        "common": {"note": "目前只有包头市公布；自治区内其余盟市以当地教育局通知为准"},
    }


def gx():
    """广西：贵港（官方校历）与玉林（市校历）。"""
    gg_comp = [span("寒假", "winter", "2027-01-24", "2027-02-21", True, "2/22 开始上课"),
               span("暑假", "summer", "2027-07-04", "2027-08-31", True)]
    gg_sen = [span("寒假", "winter", "2027-01-31", "2027-02-21", True, "2/22 开始上课"),
              span("暑假", "summer", "2027-07-11", None, True, "高三年级暑假自高考结束后开始")]
    yl_comp = [span("寒假", "winter", "2027-01-23", "2027-02-20", True, "2/21 报到，2/22 上课"),
               span("暑假", "summer", "2027-07-03", "2027-08-31", True)]
    yl_sen = [span("寒假", "winter", "2027-01-30", "2027-02-20", True, "2/21 报到，2/22 上课"),
              span("暑假", "summer", "2027-07-10", None, True, "九年级/高三以中考、高考结束时间为准")]
    cities = [
        {"code": "450800", "name": "贵港市", "policies": {
            "primary": policy("义务教育一至八年级", gg_comp, S["gg"]),
            "junior": policy("初中（七年级至八年级；九年级另计）", gg_comp, S["gg"], {"note": "九年级 2027-01-31 放假"}),
            "senior": policy("普通高中", gg_sen, S["gg"]),
        }},
        {"code": "450900", "name": "玉林市", "policies": {
            "primary": policy("义务教育一至八年级", yl_comp, S["yl"]),
            "junior": policy("初中（一至八年级口径）", yl_comp, S["yl"], {"note": "九年级 2027-01-30 放假"}),
            "senior": policy("普通高中", yl_sen, S["yl"]),
        }},
    ]
    return {
        "code": "450000", "name": "广西壮族自治区", "abbr": "广西", "kind": "province",
        "cities": cities,
        "common": {"springStart": "2027-02-22", "note": "贵港、玉林两市校历；其余地市以当地教育局通知为准"},
    }


def yn():
    """云南：目前只有「不晚于 1/16」的省级口径。"""
    hols = [span("寒假", "winter", "2027-01-16", None, True,
                 "口径为「不晚于 2027-01-16」，实际可能更早；结束日期待公布")]
    pol = {
        "primary": policy("小学", hols, S["yn"]),
        "junior": policy("初中", hols, S["yn"]),
        "senior": policy("高中", hols, S["yn"], {"note": "高中通常比义务教育晚约一周"}),
    }
    return {
        "code": "530000", "name": "云南省", "abbr": "云南", "kind": "province",
        "cities": [{"code": "530100", "name": "全省", "policies": pol}],
        "common": {"note": "省级口径为「不晚于 1 月 16 日」，精确日期需看各州市通知"},
    }


def _season(label, start, end=None, city="", scope="", note=""):
    it = {"label": label, "start": start}
    it["end"] = end or start
    if city:
        it["city"] = city
    if scope:
        it["scope"] = scope
    if note:
        it["note"] = note
    return it


def seasonal_zj():
    """浙江：2026 年秋假，各地市时间不同（区县之间还有差异）。"""
    items = [
        _season("秋假", "2026-09-28", "2026-09-30", "杭州市（主城区）",
                note="桐庐、淳安、建德为 11/12、11/13、11/16"),
        _season("秋假", "2026-11-25", "2026-11-27", "宁波市", scope="全市统一"),
        _season("秋假", "2026-11-16", "2026-11-18", "温州市（鹿城/龙湾/瓯海/洞头/乐清/永嘉）",
                note="瑞安、文成、平阳、泰顺、苍南、龙港为 11/11—11/13"),
        _season("秋假", "2026-11-12", "2026-11-16", "湖州市", scope="11/12、11/13、11/16 三天"),
        _season("秋假", "2026-11-11", "2026-11-13", "嘉兴市", scope="全市统一"),
        _season("秋假", "2026-11-11", "2026-11-13", "绍兴市（越城/柯桥/嵊州）",
                note="上虞、新昌为 11/16—11/18；诸暨为 11/13、11/16、11/17"),
        _season("秋假", "2026-11-13", "2026-11-17", "金华市", scope="11/13、11/16、11/17 三天"),
        _season("秋假", "2026-11-18", "2026-11-20", "衢州市", scope="全市统一"),
        _season("秋假", "2026-11-18", "2026-11-20", "舟山市", scope="全市统一"),
        _season("秋假", "2026-11-11", "2026-11-13", "台州市", scope="全市统一"),
        _season("秋假", "2026-11-09", "2026-11-11", "丽水市", scope="全市统一"),
    ]
    return {
        "policy": True,
        "policyText": "浙江全省推行中小学春秋假，具体时间由各市、区县定。",
        "year": 2026, "items": items, "source": S["zj_qj"],
    }


def seasonal_sd():
    """山东：2026 年秋假，12 个市已公布。"""
    items = [
        _season("秋假", "2026-11-05", "2026-11-06", "枣庄市", note="11/5、11/6、11/9 三天"),
        _season("秋假", "2026-11-09", "2026-11-09", "枣庄市"),
        _season("秋假", "2026-11-05", "2026-11-06", "济宁市", note="11/5、11/6、11/9，与周末连休 5 天"),
        _season("秋假", "2026-11-09", "2026-11-09", "济宁市"),
        _season("秋假", "2026-11-05", "2026-11-09", "青岛市", scope="义务教育学校"),
        _season("秋假", "2026-11-06", "2026-11-06", "潍坊市", note="11/6、11/9、11/10"),
        _season("秋假", "2026-11-09", "2026-11-10", "潍坊市"),
        _season("秋假", "2026-11-06", "2026-11-06", "烟台市", note="11/6、11/9、11/10"),
        _season("秋假", "2026-11-09", "2026-11-10", "烟台市"),
        _season("秋假", "2026-11-04", "2026-11-06", "日照市"),
        _season("秋假", "2026-11-04", "2026-11-06", "聊城市", note="2027 年起春假原则上安排在清明节前后"),
        _season("秋假", "2026-11-05", "2026-11-06", "滨州市", note="11/5、11/6、11/9"),
        _season("秋假", "2026-11-09", "2026-11-09", "滨州市"),
        _season("秋假", "2026-11-11", "2026-11-13", "威海市"),
        _season("秋假", "2026-11-16", "2026-11-18", "泰安市"),
        _season("秋假", "2026-11-06", "2026-11-06", "菏泽市", note="11/6、11/9、11/10"),
        _season("秋假", "2026-11-09", "2026-11-10", "菏泽市"),
        _season("秋假", "2026-11-13", "2026-11-13", "临沂市", note="11/13、11/16、11/17，与周末连休 5 天"),
        _season("秋假", "2026-11-16", "2026-11-17", "临沂市"),
    ]
    return {
        "policy": True,
        "policyText": "山东全省义务教育阶段学校实施春秋假，每次原则上 3 天，幼儿园和高中参照执行。",
        "year": 2026, "items": items, "source": S["sd_qj"],
    }


HB_HISTORY_2026 = {
    "石家庄市": {"y": 2026, "primary": "2026-02-04", "senior": "2026-02-11", "spring": "2026-03-05"},
    "唐山市": {"y": 2026, "primary": "2026-01-24", "senior": "2026-02-07", "spring": "2026-03-01",
             "note": "小学 1/24，初中 1/31，高中 2/7"},
    "秦皇岛市": {"y": 2026, "primary": "2026-01-24", "senior": "2026-01-31", "spring": "2026-03-05"},
    "邯郸市": {"y": 2026, "primary": "2026-02-03", "senior": "2026-02-10", "spring": "2026-03-05"},
    "邢台市": {"y": 2026, "primary": "2026-02-03", "senior": "2026-02-10", "spring": "2026-03-05"},
    "保定市": {"y": 2026, "primary": "2026-01-31", "senior": "2026-02-09", "spring": "2026-03-04"},
    "张家口市": {"y": 2026, "primary": "2026-01-24", "senior": "2026-01-31", "spring": "2026-03-05"},
    "承德市": {"y": 2026, "primary": "2026-01-24", "senior": "2026-02-07", "spring": "2026-03-05"},
    "沧州市": {"y": 2026, "primary": "2026-02-03", "senior": "2026-02-10", "spring": "2026-03-05"},
    "廊坊市": {"y": 2026, "primary": "2026-01-26", "senior": "2026-02-02", "spring": "2026-03-04"},
    "衡水市": {"y": 2026, "primary": "2026-02-06", "senior": "2026-02-09", "spring": "2026-03-05"},
}


def attach_history(regions):
    for r in regions:
        if r["code"] != "130000":
            continue
        for c in r["cities"]:
            h = HB_HISTORY_2026.get(c["name"])
            if h:
                c["history"] = {
                    "year": h["y"], "primary": h["primary"], "senior": h["senior"],
                    "springStart": h.get("spring", ""), "note": h.get("note", ""),
                }
        r["historySource"] = {
            "title": "河北2026年中小学寒假时间：14个地市安排一览！",
            "url": "http://www.zzwhb.com/cjwt/6140.html",
            "published": "2026-01-27",
            "grade": "aggregate",
            "publisher": "河北初中升学网（汇总各地市公布）",
        }


PENDING_NOTE = (
    "该省 2026-2027 学年中小学寒假安排，目前还没在公开渠道查到正式文件。"
    "以河北为例：2025-2026 学年的寒假安排是 2026 年 1 月下旬才陆续公布的，"
    "今年大概率也是这个节奏——不是查不到，是还没到发布的时候。"
)


def pendings():
    out = []
    for code, name in [
        ("130000", "河北省"), ("140000", "山西省"), ("220000", "吉林省"),
        ("230000", "黑龙江省"), ("410000", "河南省"), ("540000", "西藏自治区"),
        ("620000", "甘肃省"), ("630000", "青海省"), ("640000", "宁夏回族自治区"),
        ("650000", "新疆维吾尔自治区"),
    ]:
        r = {
            "code": code, "name": name,
            "abbr": name.replace("省", "").replace("市", "").replace("自治区", ""),
            "kind": "province", "cities": [],
            "pending": PENDING_NOTE,
            "pendingHint": "补数据的办法：找到省教育厅或市教育局发布的校历公告，"
                           "在 tools/build_data.py 里按现有格式加一条，重跑脚本即可。",
        }
        if code == "130000":
            r["seasonal"] = {
                "policy": True,
                "policyText": "河北已公布在全省范围内推行中小学春秋假，每次 3 天左右，具体时间由各市定。",
                "year": 2026, "items": [],
                "source": {
                    "title": "福建实施中小学春秋假！全国已有13个省份全省实施",
                    "url": "https://news.sina.com.cn/zx/gj/2026-05-07/doc-inhxaaii2864282.shtml",
                    "published": "2026-05-07", "grade": "media",
                    "publisher": "央广网（转新浪新闻）",
                },
            }
        out.append(r)
    return out


STAGE_META = [
    {"key": "primary", "label": "小学"},
    {"key": "junior", "label": "初中"},
    {"key": "senior", "label": "高中"},
    {"key": "university", "label": "大学"},
]


CN_CITIES = {
    "110000": ["北京市"],
    "120000": ["天津市"],
    "130000": ["石家庄市", "唐山市", "秦皇岛市", "邯郸市", "邢台市", "保定市",
               "张家口市", "承德市", "沧州市", "廊坊市", "衡水市"],
    "140000": ["太原市", "大同市", "阳泉市", "长治市", "晋城市", "朔州市",
               "晋中市", "运城市", "忻州市", "临汾市", "吕梁市"],
    "150000": ["呼和浩特市", "包头市", "乌海市", "赤峰市", "通辽市", "鄂尔多斯市",
               "呼伦贝尔市", "巴彦淖尔市", "乌兰察布市", "兴安盟",
               "锡林郭勒盟", "阿拉善盟"],
    "210000": ["沈阳市", "大连市", "鞍山市", "抚顺市", "本溪市", "丹东市", "锦州市",
               "营口市", "阜新市", "辽阳市", "盘锦市", "铁岭市", "朝阳市", "葫芦岛市"],
    "220000": ["长春市", "吉林市", "四平市", "辽源市", "通化市", "白山市", "松原市",
               "白城市", "延边朝鲜族自治州"],
    "230000": ["哈尔滨市", "齐齐哈尔市", "鸡西市", "鹤岗市", "双鸭山市", "大庆市",
               "伊春市", "佳木斯市", "七台河市", "牡丹江市", "黑河市", "绥化市",
               "大兴安岭地区"],
    "310000": ["上海市"],
    "320000": ["南京市", "无锡市", "徐州市", "常州市", "苏州市", "南通市", "连云港市",
               "淮安市", "盐城市", "扬州市", "镇江市", "泰州市", "宿迁市"],
    "330000": ["杭州市", "宁波市", "温州市", "嘉兴市", "湖州市", "绍兴市", "金华市",
               "衢州市", "舟山市", "台州市", "丽水市"],
    "340000": ["合肥市", "芜湖市", "蚌埠市", "淮南市", "马鞍山市", "淮北市", "铜陵市",
               "安庆市", "黄山市", "滁州市", "阜阳市", "宿州市", "六安市", "亳州市",
               "池州市", "宣城市"],
    "350000": ["福州市", "厦门市", "莆田市", "三明市", "泉州市", "漳州市", "南平市",
               "龙岩市", "宁德市"],
    "360000": ["南昌市", "景德镇市", "萍乡市", "九江市", "新余市", "鹰潭市", "赣州市",
               "吉安市", "宜春市", "抚州市", "上饶市"],
    "370000": ["济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市",
               "济宁市", "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市",
               "滨州市", "菏泽市"],
    "410000": ["郑州市", "开封市", "洛阳市", "平顶山市", "安阳市", "鹤壁市", "新乡市",
               "焦作市", "濮阳市", "许昌市", "漯河市", "三门峡市", "南阳市", "商丘市",
               "信阳市", "周口市", "驻马店市", "济源市"],
    "420000": ["武汉市", "黄石市", "十堰市", "宜昌市", "襄阳市", "鄂州市", "荆门市",
               "孝感市", "荆州市", "黄冈市", "咸宁市", "随州市", "恩施土家族苗族自治州"],
    "430000": ["长沙市", "株洲市", "湘潭市", "衡阳市", "邵阳市", "岳阳市", "常德市",
               "张家界市", "益阳市", "郴州市", "永州市", "怀化市", "娄底市",
               "湘西土家族苗族自治州"],
    "440000": ["广州市", "深圳市", "珠海市", "汕头市", "佛山市", "韶关市", "河源市",
               "梅州市", "惠州市", "汕尾市", "东莞市", "中山市", "江门市", "阳江市",
               "湛江市", "茂名市", "肇庆市", "清远市", "潮州市", "揭阳市", "云浮市"],
    "450000": ["南宁市", "柳州市", "桂林市", "梧州市", "北海市", "防城港市", "钦州市",
               "贵港市", "玉林市", "百色市", "贺州市", "河池市", "来宾市", "崇左市"],
    "460000": ["海口市", "三亚市", "儋州市", "五指山市", "琼海市", "文昌市", "万宁市",
               "东方市", "定安县", "屯昌县", "澄迈县", "临高县", "白沙黎族自治县",
               "昌江黎族自治县", "乐东黎族自治县", "陵水黎族自治县",
               "保亭黎族苗族自治县", "琼中黎族苗族自治县"],
    "500000": ["重庆市"],
    "510000": ["成都市", "自贡市", "攀枝花市", "泸州市", "德阳市", "绵阳市", "广元市",
               "遂宁市", "内江市", "乐山市", "南充市", "眉山市", "宜宾市", "广安市",
               "达州市", "雅安市", "巴中市", "资阳市", "阿坝藏族羌族自治州",
               "甘孜藏族自治州", "凉山彝族自治州"],
    "520000": ["贵阳市", "六盘水市", "遵义市", "安顺市", "毕节市", "铜仁市",
               "黔西南布依族苗族自治州", "黔东南苗族侗族自治州", "黔南布依族苗族自治州"],
    "530000": ["昆明市", "曲靖市", "玉溪市", "保山市", "昭通市", "丽江市", "普洱市",
               "临沧市", "楚雄彝族自治州", "红河哈尼族彝族自治州",
               "文山壮族苗族自治州", "西双版纳傣族自治州", "大理白族自治州",
               "德宏傣族景颇族自治州", "怒江傈僳族自治州", "迪庆藏族自治州"],
    "540000": ["拉萨市", "日喀则市", "昌都市", "林芝市", "山南市", "那曲市", "阿里地区"],
    "610000": ["西安市", "铜川市", "宝鸡市", "咸阳市", "渭南市", "延安市", "汉中市",
               "榆林市", "安康市", "商洛市"],
    "620000": ["兰州市", "嘉峪关市", "金昌市", "白银市", "天水市", "武威市", "张掖市",
               "平凉市", "酒泉市", "庆阳市", "定西市", "陇南市", "临夏回族自治州",
               "甘南藏族自治州"],
    "630000": ["西宁市", "海东市", "海北藏族自治州", "黄南藏族自治州",
               "海南藏族自治州", "果洛藏族自治州", "玉树藏族自治州",
               "海西蒙古族藏族自治州"],
    "640000": ["银川市", "石嘴山市", "吴忠市", "固原市", "中卫市"],
    "650000": ["乌鲁木齐市", "克拉玛依市", "吐鲁番市", "哈密市", "昌吉回族自治州",
               "博尔塔拉蒙古自治州", "巴音郭楞蒙古自治州", "阿克苏地区",
               "克孜勒苏柯尔克孜自治州", "喀什地区", "和田地区",
               "伊犁哈萨克自治州", "塔城地区", "阿勒泰地区"],
}


def expand_cities(regions):
    """把每个省展开成完整的城市列表。有数据的保留，没数据的显式列出。"""
    for r in regions:
        full = CN_CITIES.get(r["code"])
        if not full:
            continue
        placeholder = None
        for c in r["cities"]:
            if c["name"] == "全省":
                placeholder = c
        if placeholder is not None:
            if placeholder.get("policies"):
                r["commonPolicies"] = placeholder["policies"]
            r["cities"] = [c for c in r["cities"] if c is not placeholder]

        existing = {c["name"]: c for c in r["cities"]}
        newlist = []
        for i, name in enumerate(full):
            if name in existing:
                newlist.append(existing.pop(name))
            else:
                newlist.append({"code": r["code"][:2] + str(i).zfill(2),
                                "name": name, "policies": {}})
        for extra in existing.values():
            newlist.append(extra)
        r["cities"] = newlist


def stamp_and_cover(regions, nat, unis):
    """给每个来源补抓取时间；算出全国 31 个省级行政区的收录缺口。"""
    def tag(s):
        if s and isinstance(s, dict):
            s.setdefault("fetchedAt", FETCHED_AT)

    for r in regions:
        for c in r["cities"]:
            for p in c["policies"].values():
                tag(p.get("source"))
    tag(nat.get("source"))
    for u in unis:
        tag(u.get("source"))

    covered = {r["code"]: r["name"] for r in regions}
    with_data = [r for r in regions if r["cities"]]
    pending = [r for r in regions if not r["cities"]]
    missing = [{"code": code, "name": name}
               for code, name in ALL_PROVINCES if code not in covered]
    # 有数据但代码不在 31 省表里的（防拼错）
    unknown = [c for c in covered if c not in dict(ALL_PROVINCES)]
    if unknown:
        print("!! 收录了不在 31 省表里的代码:", unknown)
    return {
        "total": len(ALL_PROVINCES),
        "coveredCount": len(covered),
        "withDataCount": len(with_data),
        "pendingCount": len(pending),
        "missingCount": len(missing),
        "pending": [{"code": r["code"], "name": r["name"]} for r in pending],
        "missing": missing,
    }


def build():
    regions = ([bj(), sh(), cq(), gd(), sc(), js(), gz(), nm(), gx(), yn()]
               + extras() + pendings())
    regions.sort(key=lambda r: r["code"])

    # 每个省都展开成完整的地级行政区列表，省内差异才看得见
    expand_cities(regions)
    attach_history(regions)

    # 春秋假挂在省一级（各市时间不同，条目里带 city）
    for r in regions:
        if r["code"] == "330000":
            r["seasonal"] = seasonal_zj()
        elif r["code"] == "370000":
            r["seasonal"] = seasonal_sd()

    # 只被春秋假提到、但没有中小学寒假数据的城市，也补进列表里，
    # 这样点进去至少能看到春秋假和日历，而不是整座城市不存在。
    for r in regions:
        sea = r.get("seasonal")
        if not sea:
            continue
        have = set(c["name"] for c in r["cities"])
        for it in sea["items"]:
            cn = (it.get("city") or "").split("（")[0].strip()
            if not cn or cn in have:
                continue
            r["cities"].append({
                "code": r["code"][:2] + "9" + str(len(r["cities"])).zfill(2),
                "name": cn,
                "policies": {},
            })
            have.add(cn)
        r["cities"].sort(key=lambda c: c["name"])

    nat = national()
    unis = universities()
    coverage = stamp_and_cover(regions, nat, unis)

    payload = {
        "schemaVersion": 1,
        "updatedAt": UPDATED_AT.isoformat(),
        "academicYear": ACADEMIC_YEAR,
        "stageMeta": STAGE_META,
        "regions": regions,
        "nationalHolidays": nat,
        "universities": unis,
        "coverage": coverage,
        "weekendRule": {
            "rest": [0, 6],
            "note": "周日和周六（按 JS 的 Date.getDay()：0=周日、6=周六）。"
                    "中小学普遍双休，高中、寄宿制学校、毕业班可能周六上课或安排月假，以学校通知为准。",
        },
        "universityNote": (
            "普通高校的校历由各校教务处自行编制并发布，同一城市不同学校也可能不同，"
            "不存在全省统一的大学放假时间。「全部 › 高校校历」里是按学校收录的，"
            "可以单独关注；没收录的学校请直接查该校教务处官网。"
        ),
    }

    city_count = sum(len(r["cities"]) for r in regions)
    pol_count = sum(len(c["policies"]) for r in regions for c in r["cities"])
    hols = sum(len(p["holidays"]) for r in regions for c in r["cities"]
               for p in c["policies"].values())
    print(f"省份: {len(regions)}  城市/地区: {city_count}  政策条目: {pol_count}  假期: {hols}")
    print(f"法定节假日: {len(payload['nationalHolidays']['items'])}  高校: {len(payload['universities'])}")
    print(f"省级覆盖: {coverage['coveredCount']}/{coverage['total']}"
          f"（有数据 {coverage['withDataCount']} · 占位待补 {coverage['pendingCount']}）")
    if coverage["missingCount"]:
        print("未收录:", "、".join(m["name"] for m in coverage["missing"]))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=1)
    print("→", OUT, os.path.getsize(OUT), "bytes")

    bad = 0
    for r in regions:
        for c in r["cities"]:
            for sk, p in c["policies"].items():
                for h in p["holidays"]:
                    ds = d(h["start"])
                    if "end" in h and d(h["end"]) < ds:
                        print("!! 结束早于开始:", r["name"], c["name"], sk, h)
                        bad += 1
    for u in payload["universities"]:
        for h in u["holidays"]:
            if "end" in h and d(h["end"]) < d(h["start"]):
                print("!! 高校结束早于开始:", u["name"], h)
                bad += 1
    if bad:
        sys.exit(1)
    print("自检通过")


if __name__ == "__main__":
    build()
