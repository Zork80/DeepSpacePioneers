#!/usr/bin/env python3
"""
Deep Space Pioneers - OpenSCAD Model Exporter
Renders parametric SCAD game pieces to multi-material OBJ (for Unity) and STL (for 3D printing).
"""

import os
import sys
import struct
import shutil
import subprocess
import argparse
import tempfile

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

# Ordered material tags matching common.scad part() calls and TriMap.ApplyPlayerMaterial()
MATERIAL_PARTS = [
    ("hull_main", (0.85, 0.88, 0.92)),  # Replaced at runtime with Player Color
    ("hull_dark", (0.18, 0.20, 0.24)),  # Dark gunmetal armor / frames / skids
    ("accent",    (0.15, 0.72, 0.95)),  # Trim / beacons / airlock doors / feed horns
    ("glow",      (0.20, 0.95, 0.85)),  # Thrusters / cockpit visor / beacons
    ("dome",      (0.50, 0.80, 0.98)),  # Biosphere glass dome
    ("solar",     (0.10, 0.26, 0.58)),  # Solar array panels
    ("cargo",     (0.92, 0.66, 0.14)),  # Industrial cargo containers / crane
]


def parse_stl_triangles(stl_path, convert_to_unity=True):
    """Parses a binary or ASCII STL file and returns a list of triangles [(v0, v1, v2), ...]."""
    triangles = []
    if not os.path.exists(stl_path) or os.path.getsize(stl_path) == 0:
        return triangles

    is_binary = False
    with open(stl_path, "rb") as f:
        header = f.read(80)
        count_bytes = f.read(4)
        if len(header) == 80 and len(count_bytes) == 4:
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
                        ux, uy, uz = vx, vz, vy
                    else:
                        ux, uy, uz = vx, vy, vz
                    tri.append((round(ux, 3), round(uy, 3), round(uz, 3)))
                if convert_to_unity:
                    triangles.append((tri[0], tri[2], tri[1]))
                else:
                    triangles.append((tri[0], tri[1], tri[2]))
    else:
        with open(stl_path, "r", encoding="utf-8", errors="ignore") as f:
            tri = []
            for line in f:
                parts = line.strip().split()
                if len(parts) >= 4 and parts[0] == "vertex":
                    vx, vy, vz = float(parts[1]), float(parts[2]), float(parts[3])
                    if convert_to_unity:
                        ux, uy, uz = vx, vz, vy
                    else:
                        ux, uy, uz = vx, vy, vz
                    tri.append((round(ux, 3), round(uy, 3), round(uz, 3)))
                    if len(tri) == 3:
                        if convert_to_unity:
                            triangles.append((tri[0], tri[2], tri[1]))
                        else:
                            triangles.append((tri[0], tri[1], tri[2]))
                        tri = []

    return triangles


def write_mtl(mtl_path, used_tags):
    """Writes a Wavefront MTL file defining diffuse colors for all used material tags."""
    tag_colors = dict(MATERIAL_PARTS)
    with open(mtl_path, "w", encoding="utf-8") as out:
        out.write("# Deep Space Pioneers Material Library\n")
        for tag in used_tags:
            r, g, b = tag_colors.get(tag, (0.8, 0.8, 0.8))
            out.write(f"\nnewmtl {tag}\n")
            out.write(f"Kd {r:.4f} {g:.4f} {b:.4f}\n")
            out.write("Ka 0.1500 0.1500 0.1500\n")
            out.write("Ks 0.2500 0.2500 0.2500\n")
            out.write("Ns 32.0\n")
            if tag == "glow":
                out.write(f"Ke {r * 0.5:.4f} {g * 0.5:.4f} {b * 0.5:.4f}\n")


def parts_to_obj(part_stls, obj_path, convert_to_unity=True):
    """Combines multiple tagged STL meshes into one indexed multi-material OBJ + MTL."""
    coord_to_idx = {}
    unique_coords = []
    part_faces = []
    used_tags = []

    for tag, stl_path in part_stls:
        tris = parse_stl_triangles(stl_path, convert_to_unity=convert_to_unity)
        if not tris:
            continue
        used_tags.append(tag)
        faces = []
        for v0, v1, v2 in tris:
            face = []
            for key in (v0, v1, v2):
                if key not in coord_to_idx:
                    coord_to_idx[key] = len(unique_coords) + 1
                    unique_coords.append(key)
                face.append(coord_to_idx[key])
            faces.append(face)
        part_faces.append((tag, faces))

    mesh_name = os.path.splitext(os.path.basename(obj_path))[0]
    mtl_filename = f"{mesh_name}.mtl"
    mtl_path = os.path.join(os.path.dirname(obj_path), mtl_filename)
    write_mtl(mtl_path, used_tags)

    with open(obj_path, "w", encoding="utf-8") as out:
        out.write(f"# Deep Space Pioneers multi-material OBJ mesh - {mesh_name}\n")
        out.write(f"mtllib {mtl_filename}\n")
        out.write(f"o {mesh_name}\n")
        for vx, vy, vz in unique_coords:
            out.write(f"v {vx:.4f} {vy:.4f} {vz:.4f}\n")
        out.write("s 1\n")
        out.write(f"g {mesh_name}\n")
        for tag, faces in part_faces:
            out.write(f"usemtl {tag}\n")
            for v1, v2, v3 in faces:
                out.write(f"f {v1} {v2} {v3}\n")

    return used_tags


def find_openscad():
    path = shutil.which("openscad")
    if path:
        return [path]

    if shutil.which("flatpak"):
        try:
            res = subprocess.run(["flatpak", "info", "org.openscad.OpenSCAD"],
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if res.returncode == 0:
                return ["flatpak", "run", "org.openscad.OpenSCAD"]
        except Exception:
            pass

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
        sys.exit(1)

    script_dir = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.abspath(os.path.join(script_dir, args.output_dir))
    os.makedirs(out_dir, exist_ok=True)

    targets = PIECES if args.piece == "all" else [p for p in PIECES if p["name"] == args.piece]
    formats = ["obj", "stl"] if args.format == "all" else [args.format]

    print("=== Deep Space Pioneers - OpenSCAD Model Exporter ===")
    print(f"Engine:     {' '.join(scad_cmd)}")
    print(f"Resolution: $fn = {args.fn}")
    print(f"Output:     {out_dir}\n")

    with tempfile.TemporaryDirectory(dir=out_dir, prefix="_tmp_") as tmp_dir:
        for p in targets:
            src_path = os.path.join(script_dir, p["file"])
            print(f"[*] Rendering {p['name']} ({p['desc']})...")

            if "stl" in formats:
                stl_file = os.path.join(out_dir, f"{p['name']}.stl")
                cmd = scad_cmd + [
                    "--export-format", "binstl",
                    "-o", stl_file,
                    "-D", f"$fn={args.fn};",
                    src_path
                ]
                try:
                    subprocess.run(cmd, capture_output=True, text=True, check=True)
                    size_kb = os.path.getsize(stl_file) / 1024.0
                    print(f"    -> STL: {stl_file} ({size_kb:.1f} KB)")
                except subprocess.CalledProcessError as e:
                    print(f"    [FAIL] STL render failed: {e.stderr}")

            if "obj" in formats:
                part_stls = []
                for tag, _ in MATERIAL_PARTS:
                    tmp_stl = os.path.join(tmp_dir, f"{p['name']}_{tag}.stl")
                    cmd = scad_cmd + [
                        "--export-format", "binstl",
                        "-o", tmp_stl,
                        "-D", f"$fn={args.fn};",
                        "-D", f'$export_part="{tag}";',
                        src_path
                    ]
                    res = subprocess.run(cmd, capture_output=True, text=True)
                    if res.returncode == 0 and os.path.exists(tmp_stl):
                        part_stls.append((tag, tmp_stl))

                obj_file = os.path.join(out_dir, f"{p['name']}.obj")
                used_tags = parts_to_obj(part_stls, obj_file)
                size_kb = os.path.getsize(obj_file) / 1024.0
                print(f"    -> OBJ: {obj_file} ({size_kb:.1f} KB, materials: {', '.join(used_tags)})")

    print("\n[OK] Export completed!")


if __name__ == "__main__":
    main()
