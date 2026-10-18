#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PerfViewer を、ゲームなしで試すためのサンプルデータを作る(ゲームの -perf と同じ形式の CSV と meta.json)

作るもの(--out の下):
    sample_leak_<日時>/   メモリとヒープが増え続ける起動(日またぎで 2 つの CSV に分かれる)
    sample_flat_<日時>/   増えない起動。gpu_ms と gpu_temp_c は取得できなかった(-1)ことにしてある
    sample_live_<日時>/   --live のとき。1 秒に 1 行ずつ追記し続ける(リアルタイム表示の確認用)

使い方:
    python make_sample.py                       既定の出力先(%TEMP%/perf_sample)に静的なサンプルを作る
    python make_sample.py --live                上に加えて、追記し続ける起動を作る(Ctrl+C で止める)
    python make_sample.py --live --live-seconds 20
    python server.py --root <出力先>            ビューアのサーバーをこのサンプルに向ける
"""

import argparse
import json
import math
import os
import random
import tempfile
import time
from datetime import datetime, timedelta

HEADER = ("wall,uptime_s,fps,frame_ms_avg,frame_ms_max,cpu_main_ms,cpu_render_ms,cpu_process_pct,"
          "gpu_ms,gpu_pct,vram_mb,gpu_temp_c,ws_mb,private_mb,unity_alloc_mb,unity_reserved_mb,gc0,gc1,gc2,mono_used_mb,mono_heap_mb")


class Sim:
    """1 回の起動の値を、時間の関数として作る。"""

    def __init__(self, seed, leak_mb_per_hour, temp_missing=False, gpu_ms_missing=False):
        self.rng = random.Random(seed)
        self.leak = leak_mb_per_hour
        self.temp_missing = temp_missing
        self.gpu_ms_missing = gpu_ms_missing
        self.gc0 = 0
        self.gc1 = 0
        self.gc2 = 0
        self.mono = 120.0

    def row(self, wall, t):
        r = self.rng
        fps = 60.0 + r.gauss(0, 1.2)
        if r.random() < 0.01:
            fps -= r.uniform(8, 25)
        fps = max(10.0, fps)
        avg = 1000.0 / fps
        peak = avg * r.uniform(1.4, 2.5) if r.random() > 0.01 else r.uniform(35, 90)
        cpu_main = 6.0 + r.gauss(0, 0.5) + (1 + math.sin(t / 40.0))
        cpu_render = 3.0 + r.gauss(0, 0.3)
        cpu_pct = 24.0 + r.gauss(0, 2.0)
        gpu_ms = -1 if self.gpu_ms_missing else 9.0 + r.gauss(0, 0.4)
        gpu_pct = min(100.0, 68.0 + r.gauss(0, 3.0))
        vram = 5200 + 40 * math.sin(t / 90.0)
        temp = -1 if self.temp_missing else 52.0 + 22.0 * (1 - math.exp(-t / 900.0)) + r.gauss(0, 0.4)
        hours = t / 3600.0
        ws = 3100 + self.leak * hours + r.gauss(0, 3)
        private = ws + 220 + r.gauss(0, 2)
        alloc = 1400 + self.leak * 0.6 * hours + r.gauss(0, 1)
        reserved = alloc + 380
        # GC: 1 秒ごとにガベージが溜まり、一定を超えると GC が走ってマネージドヒープの使用量が戻る
        self.mono += r.uniform(0.2, 1.1)
        if self.mono > 150.0:
            self.mono = 120.0 + self.leak * 0.05 * hours
            self.gc0 += 1
            if r.random() < 0.2:
                self.gc1 += 1
            if r.random() < 0.01:
                self.gc2 += 1
        heap = 190.0 + self.leak * 0.05 * hours + (30 if hours > 0.25 else 0)
        values = [fps, avg, peak, cpu_main, cpu_render, cpu_pct, gpu_ms, gpu_pct, vram, temp, ws, private, alloc, reserved,
                  self.gc0, self.gc1, self.gc2, self.mono, heap]
        return format_row(wall, t, values)


def fmt(value, decimals):
    if value < 0:
        return "-1"
    return ("%." + str(decimals) + "f") % value


def format_row(wall, t, v):
    decimals = [1, 2, 2, 2, 2, 1, 2, 1, 0, 0, 1, 1, 1, 1, None, None, None, 1, 1]
    cells = [wall.strftime("%Y-%m-%dT%H:%M:%S"), fmt(t, 2)]
    for value, d in zip(v, decimals):
        cells.append(str(int(value)) if d is None else fmt(value, d))
    return ",".join(cells)


def write_meta(folder, label, start, development=False):
    meta = {
        "schema": 1, "label": label, "start": start.strftime("%Y-%m-%dT%H:%M:%S"), "interval_seconds": 1.0, "columns": HEADER,
        "arguments": "sample -perf -perflabel " + label, "app_version": "0.24.0", "unity_version": "6000.6.5f1",
        "development_build": development, "is_editor": False, "scripting_backend": "Mono", "os": "Windows 11 (sample)",
        "cpu": "sample CPU", "cpu_threads": 24, "system_memory_mb": 65536, "gpu": "NVIDIA GeForce RTX 5060 Ti (sample)",
        "gpu_api": "Direct3D12", "gpu_memory_mb": 16311, "nvml_available": True, "nvml_device": "sample", "frame_timing_enabled": True,
        "render_pipeline": "HDRenderPipelineAsset", "quality_level": "AimRacing", "vsync_count": 0, "target_frame_rate": -1,
        "screen": "2560x1440", "fullscreen_mode": "FullScreenWindow", "displays": 3, "display_sizes": "2560x1440,2560x1440,2560x1440",
        "clean_exit": True,
    }
    with open(os.path.join(folder, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)


def make_static(out, label, start, minutes, sim):
    folder = os.path.join(out, "%s_%s" % (label, start.strftime("%Y%m%d_%H%M%S")))
    os.makedirs(folder, exist_ok=True)
    write_meta(folder, label, start)
    files = {}
    for second in range(minutes * 60):
        wall = start + timedelta(seconds=second)
        name = "perf_%s.csv" % wall.strftime("%Y%m%d")
        if name not in files:
            files[name] = open(os.path.join(folder, name), "w", encoding="utf-8", newline="")
            files[name].write(HEADER + "\n")
        files[name].write(sim.row(wall, second) + "\n")
    for f in files.values():
        f.close()
    return folder


def make_live(out, seconds):
    start = datetime.now().replace(microsecond=0)
    label = "sample_live"
    folder = os.path.join(out, "%s_%s" % (label, start.strftime("%Y%m%d_%H%M%S")))
    os.makedirs(folder, exist_ok=True)
    write_meta(folder, label, start)
    sim = Sim(3, leak_mb_per_hour=900.0)
    path = os.path.join(folder, "perf_%s.csv" % start.strftime("%Y%m%d"))
    print("追記を始めました(Ctrl+C で止める): " + folder)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(HEADER + "\n")
        f.flush()
        t = 0
        try:
            while seconds <= 0 or t < seconds:
                f.write(sim.row(datetime.now(), t) + "\n")
                f.flush()
                t += 1
                time.sleep(1.0)
        except KeyboardInterrupt:
            pass
    print("止めました。行数: %d" % t)


def main():
    parser = argparse.ArgumentParser(description="PerfViewer を試すためのサンプルデータを作る")
    parser.add_argument("--out", default=os.path.join(tempfile.gettempdir(), "perf_sample"), help="出力先(既定: %(default)s)")
    parser.add_argument("--minutes", type=int, default=30, help="静的なサンプルの長さ(分)")
    parser.add_argument("--live", action="store_true", help="1 秒に 1 行ずつ追記し続ける起動も作る")
    parser.add_argument("--live-seconds", type=int, default=0, help="--live の長さ(秒)。0 なら Ctrl+C まで")
    args = parser.parse_args()

    os.makedirs(args.out, exist_ok=True)
    now = datetime.now().replace(microsecond=0)

    # 日またぎ: 今日の 0 時の 15 分前に始めて、2 つの CSV に分かれるようにする
    midnight = now.replace(hour=0, minute=0, second=0)
    start_a = midnight - timedelta(minutes=15)
    start_b = now - timedelta(minutes=args.minutes + 5)
    print("作りました: " + make_static(args.out, "sample_leak", start_a, args.minutes, Sim(1, leak_mb_per_hour=1200.0)))
    print("作りました: " + make_static(args.out, "sample_flat", start_b, args.minutes, Sim(2, leak_mb_per_hour=0.0, temp_missing=True, gpu_ms_missing=True)))
    print("サーバーの起動: python server.py --root \"%s\"" % args.out)

    if args.live:
        make_live(args.out, args.live_seconds)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
