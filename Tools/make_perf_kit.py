#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
別の PC へ持っていく一式(PerfKit.zip)を作る。プロジェクトのルートで:

    python Tools\\make_perf_kit.py

中身(プロジェクトのルートからの相対パスのまま入る。プロジェクトのルートへ展開すればそのまま使える):
    Assets/#Scripts/Perf/*.cs      ゲーム側の記録(別の PC でビルドするときに使う)
    Tools/PerfViewer/*             グラフ表示と、起動・収集のスクリプト
    docs/PerfMeasurement.md        使い方
    PerfKit_INFO.txt               作成日時と、各ファイルの大きさ・SHA-256(別の PC で同じ内容かを確かめる)

ファイルを直したら、もう一度実行して作り直す。
"""

import datetime
import hashlib
import os
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUTPUT = os.path.join(ROOT, "Tools", "PerfKit.zip")

SOURCES = [
    ("Assets/#Scripts/Perf", (".cs",)),
    ("Tools/PerfViewer", (".py", ".html", ".bat", ".ps1")),
]
SINGLE_FILES = ["docs/PerfMeasurement.md"]


def collect():
    files = []
    for folder, extensions in SOURCES:
        base = os.path.join(ROOT, *folder.split("/"))
        if not os.path.isdir(base):
            raise SystemExit("見つかりません: " + base)
        for name in sorted(os.listdir(base)):
            if name.lower().endswith(extensions):
                files.append(folder + "/" + name)
    for rel in SINGLE_FILES:
        if not os.path.isfile(os.path.join(ROOT, *rel.split("/"))):
            raise SystemExit("見つかりません: " + rel)
        files.append(rel)
    return files


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    files = collect()
    lines = [
        "PerfKit  作成日時: " + datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "プロジェクトのルートへ展開すると、元と同じ場所に置かれる。使い方は docs/PerfMeasurement.md",
        "同じ内容かの確認: PowerShell で  Get-FileHash -Algorithm SHA256 <ファイル>  を実行して、下の値と比べる",
        "",
    ]
    for rel in files:
        path = os.path.join(ROOT, *rel.split("/"))
        lines.append("%s  %9d B  %s" % (sha256(path), os.path.getsize(path), rel))
    info = "\n".join(lines) + "\n"

    with zipfile.ZipFile(OUTPUT, "w", zipfile.ZIP_DEFLATED) as z:
        for rel in files:
            z.write(os.path.join(ROOT, *rel.split("/")), rel)
        z.writestr("PerfKit_INFO.txt", info.encode("utf-8"))

    print("作りました: " + OUTPUT + "  (%d ファイル、%.1f KB)" % (len(files) + 1, os.path.getsize(OUTPUT) / 1024))
    for rel in files:
        print("  " + rel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
