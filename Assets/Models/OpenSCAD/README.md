# Deep Space Pioneers – Parametrische OpenSCAD Spielfiguren

Dieses Verzeichnis enthält die parametrischen 3D-Modelle für alle Spielfiguren und Außenposten von *Deep Space Pioneers*.

Die Modelle sind sowohl für die Echtzeit-Darstellung in Unity (Export als `.obj`) als auch für den physikalischen 3D-Druck (Export als `.stl`, optimiert für FDM- und Resin-Drucker wie Bambu Lab / Prusa via OrcaSlicer) konzipiert.

---

## 🚀 Übersicht der Spielfiguren

| Modell | Datei | Beschreibung | Besonderheiten |
|---|---|---|---|
| **Kolonie (Colony)** | `colony.scad` | Grundlegende planetare Siedlung (Tier 1) | Hexagonaler Sockel, 3 Schleusentrakte im 120°-Winkel passend zum Dreiecks-/Hexgitter, Biodom mit Exoskelett-Bögen, Solarpaneel |
| **Raumhafen (Spaceport)** | `spaceport.scad` | Ausbaustufe der Kolonie (Tier 2) | Umschließt die Kolonie mit 3 massiven Startpylonen, oberem Orbital-Dockingring, Betankungsarm und Subraum-Sensorantenne |
| **Handelsstation (Trade Base)** | `trade_station.scad` | Neutraler oder errichteter Handelsposten | Zwillings-Parabolantennen, modulare Frachtcontainer-Paletten, Flugleitturm mit Panoramakuppel |
| **Kolonieschiff (Colony Ship)** | `colony_ship.scad` | Transporter zur Koloniegründung | Sci-Fi Keilrumpf mit Warpgondeln und eingeklinkter Biosphären-Siedlungskapsel |
| **Handelsschiff (Trade Ship)** | `trade_ship.scad` | Transporter für den interstellaren Handel | Gleicher modularer Rumpf wie das Kolonieschiff, beladen mit 3 sechseckigen Schwerlast-Frachtcontainern |

---

## 📐 Design & Skalierung

- **Gitter-Ausrichtung**: Kolonie und Raumhafen besitzen einen hexagonalen Sockel mit 120°-Achsen, sodass Straßen/Flugrouten und Kanten perfekt an die Dreiecksfelder anschließen.
- **Modulare Schiffs-Architektur**: Kolonieschiff und Handelsschiff teilen sich den aerodynamischen Rumpf (`ship_hull_chassis` in `ships.scad`) und unterscheiden sich durch ihre spezifische Nutzlast (`colony_payload` vs. `trade_payload`).
- **Maßstab**:
  - Im Code entsprechen die Einheiten Millimetern (z. B. Koloniedurchmesser 24 mm).
  - Für Unity entspricht 1 Einheit = 10 mm (bzw. Skalierung im Prefab ca. 0.25).
  - Für 3D-Druck (OrcaSlicer/Bambu Studio/PrusaSlicer) direkt 1:1 druckbar.

---

## 🛠️ Export nach Unity & 3D-Druck

### Automatisiert per Python-Skript:
Das beiliegende Skript `export_pieces.py` erkennt installierte OpenSCAD-Installationen (native CLI, Flatpak oder AppImage) und erzeugt die `.obj`- und `.stl`-Dateien in `Assets/Models/Pieces/`:

```bash
# Alle Modelle rendern ($fn=64)
python3 export_pieces.py

# Nur OBJ für Unity rendern
python3 export_pieces.py --format obj

# Spezifische Figur mit hoher Auflösung für den 3D-Druck rendern
python3 export_pieces.py --piece colony_ship --format stl --fn 96
```

### Manuell per OpenSCAD CLI:
```bash
openscad -o ../Pieces/colony.obj -D '$fn=64;' colony.scad
openscad -o ../Pieces/spaceport.obj -D '$fn=64;' spaceport.scad
openscad -o ../Pieces/trade_station.obj -D '$fn=64;' trade_station.scad
openscad -o ../Pieces/colony_ship.obj -D '$fn=64;' colony_ship.scad
openscad -o ../Pieces/trade_ship.obj -D '$fn=64;' trade_ship.scad
```
