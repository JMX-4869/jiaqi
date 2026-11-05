# -*- coding: utf-8 -*-
"""
把 data/holidays.json 发布到 GitHub 仓库，让三端能自动拉取。

三端里写死的更新地址是：
    https://raw.githubusercontent.com/JMX-4869/jiaqi-data/master/holidays.json

用法：
    python tools/publish.py             # 推送当前 data/holidays.json
    python tools/publish.py --check     # 只检查远端是不是最新的

依赖：git、gh（已登录）
"""

import argparse
import filecmp
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, "data", "holidays.json")
REPO_DIR = os.path.join(ROOT, ".data-repo")
REMOTE = "https://github.com/JMX-4869/jiaqi-data.git"
RAW = "https://raw.githubusercontent.com/JMX-4869/jiaqi-data/master/holidays.json"


def run(cmd, cwd=None, check=True):
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    if check and p.returncode != 0:
        print("命令失败:", " ".join(cmd))
        print(p.stdout)
        print(p.stderr)
        sys.exit(1)
    return p


def ensure_repo():
    if not os.path.isdir(os.path.join(REPO_DIR, ".git")):
        print("克隆数据仓库 →", REPO_DIR)
        run(["git", "clone", "-q", REMOTE, REPO_DIR])
    else:
        run(["git", "pull", "-q", "--ff-only"], cwd=REPO_DIR, check=False)
    return REPO_DIR


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    if not os.path.exists(SRC):
        print("找不到 data/holidays.json，先跑 tools/build_data.py")
        return 1

    repo = ensure_repo()
    dst = os.path.join(repo, "holidays.json")

    if os.path.exists(dst) and filecmp.cmp(SRC, dst, shallow=False):
        print("远端已经是最新的，没有变化。")
        print("地址:", RAW)
        return 0

    if args.check:
        print("远端不是最新（本地文件有变化，需要 publish）")
        return 2

    shutil.copyfile(SRC, dst)
    run(["git", "add", "holidays.json"], cwd=repo)
    run(["git", "-c", "user.name=JMX-4869",
         "-c", "user.email=JMX-4869@users.noreply.github.com",
         "commit", "-qm", "update holidays.json"], cwd=repo, check=False)

    p = run(["git", "push", "-q", "origin", "master"], cwd=repo, check=False)
    if p.returncode != 0:
        print("推送失败：")
        print(p.stderr)
        return 1

    size = os.path.getsize(SRC)
    print(f"已发布 {size} 字节")
    print("地址:", RAW)
    return 0


if __name__ == "__main__":
    sys.exit(main())
