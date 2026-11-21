# Deep Space Pioneers – Parametric OpenSCAD Game Pieces

This directory contains the parametric 3D models for all game pieces and outposts of *Deep Space Pioneers*.

The models are designed both for real-time rendering in Unity (exported as `.obj`) and for physical 3D printing (exported as `.stl`, optimized for FDM and resin printers such as Bambu Lab / Prusa via OrcaSlicer).

---

## 🚀 Game Piece Overview

| Model | File | Description | Key Features |
|---|---|---|---|
| **Colony** | `colony.scad` | Basic planetary settlement (Tier 1) | Hexagonal base, 3 airlock wings at 120° angles matching the triangle/hex grid, biodome with exoskeleton arches, solar panel |
| **Spaceport** | `spaceport.scad` | Colony upgrade (Tier 2) | Encloses the colony with 3 heavy launch pylons, upper orbital docking ring, fueling arm, and subspace sensor antenna |
| **Trade Station** | `trade_station.scad` | Neutral or player-built trading outpost | Twin parabolic dishes, modular cargo container pallets, flight control tower with panoramic dome |
| **Colony Ship** | `colony_ship.scad` | Transport vessel for founding colonies | Sci-fi wedge hull with warp nacelles and docked biosphere settlement pod |
| **Trade Ship** | `trade_ship.scad` | Transport vessel for interstellar trade | Same modular hull as the colony ship, loaded with 3 hexagonal heavy-duty cargo containers |

---

## 📐 Design & Scaling

- **Grid Alignment**: Colony and Spaceport feature a hexagonal base with 120° axes so that flight routes and edges align cleanly with the triangular grid cells.
- **Modular Ship Architecture**: Colony Ship and Trade Ship share the aerodynamic hull (`ship_hull_chassis` in `ships.scad`) and differ by their specific payload (`colony_payload` vs. `trade_payload`).
- **Scale**:
  - In the OpenSCAD code, units correspond to millimeters (e.g., colony diameter 24 mm).
  - For Unity, 1 unit = 10 mm (or prefab scale approx. 0.25).
  - For 3D printing (OrcaSlicer / Bambu Studio / PrusaSlicer), directly printable at 1:1 scale.

---

## 🛠️ Exporting for Unity & 3D Printing

### Automated via Python Script:
The included `export_pieces.py` script detects installed OpenSCAD binaries (native CLI, Flatpak, or AppImage) and generates the `.obj` and `.stl` files in `Assets/Models/Pieces/`:

```bash
# Render all models ($fn=64)
python3 export_pieces.py

# Render OBJ only for Unity
python3 export_pieces.py --format obj

# Render a specific piece in high resolution for 3D printing
python3 export_pieces.py --piece colony_ship --format stl --fn 96
```

### Manual via OpenSCAD CLI:
```bash
openscad -o ../Pieces/colony.obj -D '$fn=64;' colony.scad
openscad -o ../Pieces/spaceport.obj -D '$fn=64;' spaceport.scad
openscad -o ../Pieces/trade_station.obj -D '$fn=64;' trade_station.scad
openscad -o ../Pieces/colony_ship.obj -D '$fn=64;' colony_ship.scad
openscad -o ../Pieces/trade_ship.obj -D '$fn=64;' trade_ship.scad
```
