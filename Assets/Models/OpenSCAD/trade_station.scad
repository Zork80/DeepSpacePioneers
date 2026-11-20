// ====================================================================
// Deep Space Pioneers - Trade Base Piece (Handelsstation)
// Orbital or planetary commercial trading outpost with cargo bays
// (No hex plate, rests on grid)
// ====================================================================

include <common.scad>

module trade_station_piece(
    platform_r = 11.0,
    platform_h = 1.4,
    hub_h = 9.0
) {
    // 1. Circular Docking Hub Ring (Ground collar)
    color(COLOR_HULL_DARK) {
        difference() {
            cylinder(r1=platform_r, r2=platform_r - 0.6, h=platform_h);
            // Inner docking slots
            translate([0, 0, 0.4])
                cylinder(r=platform_r - 1.2, h=2.0);
        }
    }

    // 2. Central Operations Spindle & Traffic Control Tower
    translate([0, 0, 0]) {
        // Base collar
        color(COLOR_HULL_DARK)
            cylinder(r1=4.5, r2=3.6, h=2.2);

        // Core Spindle
        color(COLOR_HULL_MAIN)
            translate([0, 0, 1.8])
                cylinder(r=3.0, h=hub_h);

        // Observation / Traffic Control Ring at Top
        translate([0, 0, hub_h - 1.2]) {
            color(COLOR_HULL_MAIN)
                cylinder(r1=3.0, r2=5.0, h=1.6);
            // Panoramic Window Ring
            color(COLOR_GLOW)
                translate([0, 0, 1.6])
                    cylinder(r=4.8, h=1.0);
            color(COLOR_HULL_MAIN)
                translate([0, 0, 2.6])
                    cylinder(r1=5.0, r2=3.2, h=0.8);
        }

        // Subspace Comm Array on Spire
        translate([0, 0, hub_h + 2.2]) {
            color(COLOR_HULL_DARK)
                cylinder(r=0.6, h=3.0);
            translate([0, 0, 3.0])
                color(COLOR_ACCENT)
                    sphere(r=0.9);
        }
    }

    // 3. Dual High-Gain Subspace Satellite Dishes (Twin Antennas)
    for (i = [0, 1]) {
        rotate([0, 0, 90 + i*180]) {
            translate([platform_r*0.62, 0, 0.6]) {
                // Turret Base
                color(COLOR_HULL_DARK)
                    cylinder(r=1.6, h=1.6);
                // Angled Gimbals
                translate([0, 0, 1.6])
                    rotate([0, 30, i == 0 ? 25 : -25])
                        sensor_dish(dish_r=3.8, depth=1.3, stem_h=2.2);
            }
        }
    }

    // 4. Modular Cargo Containers & Pallet Racks (Industrial Trading Flavor)
    for (ang = [30, 150, 270]) {
        rotate([0, 0, ang]) {
            translate([platform_r*0.52, 0, 0.5]) {
                // Cargo Pallet Platform
                color(COLOR_HULL_DARK)
                    translate([-2.5, -2.0, 0])
                        cube([5.0, 4.0, 0.6]);

                // Stacked Standardized Freight Containers (TEU Pods)
                color(COLOR_CARGO) {
                    translate([-2.0, -1.8, 0.6])
                        cube([4.0, 1.6, 1.8]);
                    translate([-2.0, 0.2, 0.6])
                        cube([4.0, 1.6, 1.8]);
                }
                // Top single container
                color(COLOR_ACCENT)
                    translate([-1.8, -0.9, 2.4])
                        cube([3.6, 1.8, 1.6]);
            }
        }
    }

    // 5. Docking Clamps & Light Beacons for Ships
    for (ang = [0, 120, 240]) {
        rotate([0, 0, ang]) {
            translate([platform_r - 1.0, 0, 0.4]) {
                color(COLOR_HULL_DARK)
                    cube([1.4, 2.8, 1.4], center=true);
                color(COLOR_GLOW)
                    translate([0.4, 0, 0.7])
                        sphere(r=0.4);
            }
        }
    }
}

// Standalone Render
trade_station_piece();
