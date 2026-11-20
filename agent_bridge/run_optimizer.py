#!/usr/bin/env python3
import os
import sys
import time
import subprocess
import json
from pathlib import Path

WORKSPACE = Path("/home/florian/Workspace/TriangleTest")
BUILD_TRIGGER = WORKSPACE / "Temp" / "trigger_build.txt"
BUILD_DONE = WORKSPACE / "Temp" / "build_done.txt"
BUILD_DLL = WORKSPACE / "Builds" / "Linux_Client" / "DeepSpacePioneers_Data" / "Managed" / "Assembly-CSharp.dll"
EDITOR_LOG = Path(os.path.expanduser("~/.config/unity3d/Editor.log"))
HOST_LOG = WORKSPACE / "host_player.log"
CLIENT_LOG = WORKSPACE / "client_player.log"

def trigger_build(timeout=45.0):
    if BUILD_DONE.exists():
        BUILD_DONE.unlink()
    BUILD_TRIGGER.touch()
    start = time.time()
    while time.time() - start < timeout:
        time.sleep(0.5)
        if BUILD_DONE.exists():
            status = BUILD_DONE.read_text().strip()
            if status.startswith("SUCCESS"):
                return True
            else:
                print(f"[Optimizer] Build reported error: {status}")
                return False
    return False

def get_perf_stats(log_path):
    fps_list = []
    ms_list = []
    max_ms_list = []
    cam_fps_list = []
    if log_path.exists():
        with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                if "[PERF]" in line or "[PERF-CAM]" in line:
                    parts = line.strip().split("|")
                    try:
                        fps = float(parts[0].split(":")[1].replace(",", ".").strip())
                        ms = float(parts[1].split(":")[1].replace("ms", "").replace(",", ".").strip())
                        max_ms = 0.0
                        for p in parts:
                            if "MaxMs:" in p:
                                max_ms = float(p.split(":")[1].replace("ms", "").replace(",", ".").strip())
                        if fps > 5:
                            fps_list.append(fps)
                            ms_list.append(ms)
                            if max_ms > 0:
                                max_ms_list.append(max_ms)
                            if "[PERF-CAM]" in line:
                                cam_fps_list.append(fps)
                    except:
                        pass
    avg_fps = sum(fps_list) / len(fps_list) if fps_list else 0.0
    avg_ms = sum(ms_list) / len(ms_list) if ms_list else 0.0
    peak_ms = max(max_ms_list) if max_ms_list else 0.0
    cam_fps = sum(cam_fps_list) / len(cam_fps_list) if cam_fps_list else 0.0
    return avg_fps, avg_ms, peak_ms, cam_fps

def run_test_and_profile(duration=20.0, benchmark_camera=True):
    if HOST_LOG.exists(): HOST_LOG.unlink()
    if CLIENT_LOG.exists(): CLIENT_LOG.unlink()

    extra = "-benchmark-camera" if benchmark_camera else ""
    cmd = [str(WORKSPACE / "run_lan_two_bots.sh"), "42", "7777", "0.3", extra]
    proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    # Wait for both host and client to finish launching, connecting, and reach active gameplay turns
    time.sleep(13.0)

    # Sample instantaneous CPU using top
    try:
        # Get PIDs of DeepSpacePioneers
        pids = subprocess.check_output(["pgrep", "DeepSpacePionee"], text=True).strip().split()
        if pids:
            pid_arg = ",".join(pids)
            top_out = subprocess.check_output(["top", "-b", "-n", "2", "-d", "1.0", "-p", pid_arg], text=True)
            iterations = top_out.split("top - ")
            ps_out = iterations[-1] if len(iterations) > 1 else top_out
        else:
            ps_out = "No DeepSpacePioneers processes found"
    except Exception as ex:
        ps_out = f"CPU sampling error: {ex}"

    time.sleep(max(1.0, duration - 15.0))

    # Terminate cleanly
    subprocess.run(["killall", "-9", "DeepSpacePioneers.x86_64", "autonomous_player.py"], stderr=subprocess.DEVNULL)
    proc.kill()
    proc.wait()

    avg_fps, avg_ms, peak_ms, cam_fps = get_perf_stats(HOST_LOG)
    client_fps, client_ms, client_peak, _ = get_perf_stats(CLIENT_LOG)
    return ps_out, avg_fps, avg_ms, peak_ms, cam_fps, client_fps, client_ms

if __name__ == "__main__":
    print("[Optimizer] Starting autonomous optimization iteration with CAMERA BENCHMARK...")
    ok = trigger_build()
    print(f"[Optimizer] Build Success: {ok}")
    ps, fps, ms, peak_ms, cam_fps, c_fps, c_ms = run_test_and_profile(26.0, benchmark_camera=True)
    print(f"[Optimizer] Host (with Camera Motion): FPS={fps:.1f} (CamFPS={cam_fps:.1f}), AvgFrame={ms:.2f}ms, PeakStutter={peak_ms:.2f}ms")
    print(f"[Optimizer] Client: FPS={c_fps:.1f}, AvgFrame={c_ms:.2f}ms")
    print(f"[Optimizer] Process CPU Profile:\n{ps}")
