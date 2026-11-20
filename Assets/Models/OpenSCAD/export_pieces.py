#!/usr/bin/env python3
"""
Deep Space Pioneers - OpenSCAD Model Exporter
Renders parametric SCAD game pieces to OBJ and STL meshes for Unity and 3D printing.
"""

import os
import sys
import struct
import shutil
import subprocess
import argparse

PIECES = [
    {
        "name": "colony",
        "file": "colony.scad",
        "desc": "Planetary Colony settlement dome & airlock hub"
    },
    {
        "name": "spaceport",
        "file": "spaceport.scad",
        "desc": "Upgraded Spaceport with orbital gantry towers and radar"
    },
    {
        "name": "trade_station",
        "file": "trade_station.scad",
        "desc": "Trade Station with subspace dishes and cargo pods"
    },
    {
        "name": "colony_ship",
        "file": "colony_ship.scad",
        "desc": "Colony Transporter ship carrying settlement capsule"
    },
    {
        "name": "trade_ship",
        "file": "trade_ship.scad",
        "desc": "Trade Transporter ship carrying cargo containers"
    }
]

def stl_to_obj(stl_path, obj_path, convert_to_unity=True):
    """Converts binary or ASCII STL into an indexed OBJ mesh with shared vertices.
    When convert_to_unity=True, maps OpenSCAD (+Y forward, +Z up) to Unity (+Z forward, +Y up)
    and swaps triangle winding order so normals point outward.
    """
    coord_to_idx = {}
    unique_coords = []
    faces = []

    is_binary = False
    with open(stl_path, "rb") as f:
        f.seek(80)
        count_bytes = f.read(4)
        if len(count_bytes) == 4:
            num_triangles = struct.unpack("<I", count_bytes)[0]
            if os.path.getsize(stl_path) == 80 + 4 + num_triangles * 50:
                is_binary = True

    if is_binary:
        with open(stl_path, "rb") as f:
            f.seek(84)
            for _ in range(num_triangles):
                data = f.read(50)
                if len(data) < 50:
                    break
                tri = []
                for v_offset in (12, 24, 36):
                    vx, vy, vz = struct.unpack("<3f", data[v_offset : v_offset + 12])
                    if convert_to_unity:
                        ux = vx
                        uy = vz
                        uz = vy
                    else:
                        ux, uy, uz = vx, vy, vz
                    key = (round(ux, 3), round(uy, 3), round(uz, 3))
                    if key not in coord_to_idx:
                        coord_to_idx[key] = len(unique_coords) + 1
                        unique_coords.append(key)
                    tri.append(coord_to_idx[key])
                if convert_to_unity:
                    faces.append([tri[0], tri[2], tri[1]])
                else:
                    faces.append(tri)
    else:
        with open(stl_path, "r", encoding="utf-8", errors="ignore") as f:
            tri = []
            for line in f:
                parts = line.strip().split()
                if len(parts) >= 4 and parts[0] == "vertex":
                    vx = float(parts[1])
                    vy = float(parts[2])
                    vz = float(parts[3])
                    if convert_to_unity:
                        ux = vx
                        uy = vz
                        uz = vy
                    else:
                        ux, uy, uz = vx, vy, vz
                    key = (round(ux, 3), round(uy, 3), round(uz, 3))
                    if key not in coord_to_idx:
                        coord_to_idx[key] = len(unique_coords) + 1
                        unique_coords.append(key)
                    tri.append(coord_to_idx[key])
                    if len(tri) == 3:
                        if convert_to_unity:
                            faces.append([tri[0], tri[2], tri[1]])
                        else:
                            faces.append(tri)
                        tri = []

    mesh_name = os.path.splitext(os.path.basename(obj_path))[0]
    with open(obj_path, "w", encoding="utf-8") as out:
        out.write(f"# Deep Space Pioneers OBJ mesh - {mesh_name}\n")
        out.write(f"o {mesh_name}\n")
        for vx, vy, vz in unique_coords:
            out.write(f"v {vx:.4f} {vy:.4f} {vz:.4f}\n")
        out.write("s 1\n")
        for v1, v2, v3 in faces:
            out.write(f"f {v1} {v2} {v3}\n")
    return True

def find_openscad():
    # 1. Native CLI in PATH
    path = shutil.which("openscad")
    if path:
        return [path]

    # 2. Flatpak installation
    if shutil.which("flatpak"):
        try:
            res = subprocess.run(["flatpak", "info", "org.openscad.OpenSCAD"],
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if res.returncode == 0:
                return ["flatpak", "run", "org.openscad.OpenSCAD"]
        except Exception:
            pass

    # 3. Known Snap / AppImage / Custom locations
    candidates = [
        "/snap/bin/openscad",
        "/var/lib/snapd/snap/bin/openscad",
        os.path.expanduser("~/Applications/OpenSCAD.AppImage"),
        os.path.expanduser("~/.local/bin/openscad"),
        "/usr/local/bin/openscad"
    ]
    for c in candidates:
        if os.path.isfile(c) and os.access(c, os.X_OK):
            return [c]

    return None

def main():
    parser = argparse.ArgumentParser(description="Export OpenSCAD models for Deep Space Pioneers")
    parser.add_argument("--format", choices=["obj", "stl", "all"], default="all",
                        help="Export format (obj, stl, or all)")
    parser.add_argument("--fn", type=int, default=64,
                        help="Mesh resolution ($fn), default: 64")
    parser.add_argument("--output-dir", default="../Pieces",
                        help="Output directory relative to script (default: ../Pieces)")
    parser.add_argument("--piece", choices=[p["name"] for p in PIECES] + ["all"], default="all",
                        help="Specific piece to render")
    args = parser.parse_args()

    scad_cmd = find_openscad()
    if not scad_cmd:
        print("[ERROR] OpenSCAD binary or flatpak was not found on this system.")
        print("To install OpenSCAD:")
        print("  - Ubuntu/Debian:  sudo apt install openscad")
        print("  - Flatpak:        flatpak install flathub org.openscad.OpenSCAD")
        print("  - AppImage:       Download from https://openscad.org/downloads.html")
        sys.exit(1)

    script_dir = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.abspath(os.path.join(script_dir, args.output_dir))
    os.makedirs(out_dir, exist_ok=True)

    targets = PIECES if args.piece == "all" else [p for p in PIECES if p["name"] == args.piece]
    formats = ["obj", "stl"] if args.format == "all" else [args.format]

    print(f"=== Deep Space Pioneers - OpenSCAD Model Exporter ===")
    print(f"Engine:     {' '.join(scad_cmd)}")
    print(f"Resolution: $fn = {args.fn}")
    print(f"Output:     {out_dir}\n")

    for p in targets:
        src_path = os.path.join(script_dir, p["file"])
        print(f"[*] Rendering {p['name']} ({p['desc']})...")

        stl_file = os.path.join(out_dir, f"{p['name']}.stl")
        cmd = scad_cmd + [
            "--export-format", "binstl",
            "-o", stl_file,
            "-D", f"$fn={args.fn};",
            src_path
        ]
        try:
            subprocess.run(cmd, capture_output=True, text=True, check=True)
            if "stl" in formats:
                size_kb = os.path.getsize(stl_file) / 1024.0
                print(f"    -> STL: {stl_file} ({size_kb:.1f} KB)")
            if "obj" in formats:
                obj_file = os.path.join(out_dir, f"{p['name']}.obj")
                stl_to_obj(stl_file, obj_file)
                size_kb = os.path.getsize(obj_file) / 1024.0
                print(f"    -> OBJ: {obj_file} ({size_kb:.1f} KB)")
            if "stl" not in formats and os.path.exists(stl_file):
                os.remove(stl_file)
        except subprocess.CalledProcessError as e:
            print(f"    [FAIL] Could not render piece: {e.stderr}")

    print("\n[OK] Export completed!")

if __name__ == "__main__":
    main()
