// ====================================================================
// Deep Space Pioneers - Spaceport Piece (Raumhafen)
// Upgraded Colony (Tier-2 Settlement with Orbital Gantry & Radar)
// Uses the Colony at its core, enhanced with orbital infrastructure.
// ====================================================================

include <common.scad>
use <colony.scad>

module spaceport_piece(
    dome_r = 7.0,
    airlock_len = 6.5,
    airlock_w = 3.6,
    gantry_r = 8.8,
    gantry_h = 11.5,
    tower_w = 2.0
) {
    // ----------------------------------------------------------------
    // 1. The Core Colony Settlement (Identical base architecture)
    // ----------------------------------------------------------------
    colony_piece(
        dome_r = dome_r,
        airlock_len = airlock_len,
        airlock_w = airlock_w,
        show_details = true
    );

    // ----------------------------------------------------------------
    // 2. Upgraded Habitation Pods (Tier-2 expansion on the 3 wings)
    // ----------------------------------------------------------------
    // Secondary observation domes seated atop each of the 3 airlocks
    for (ang = [0, 120, 240]) {
        rotate([0, 0, ang]) {
            translate([dome_r + airlock_len * 0.55, 0, 2.5]) {
                // Habitation Blister Dome
                part("dome", COLOR_DOME)
                    difference() {
                        sphere(r=2.0);
                        translate([0, 0, -2.0])
                            cube([5.0, 5.0, 4.0], center=true);
                    }
                // Collar Ring
                part("hull_main", COLOR_HULL_MAIN)
                    cylinder(r=2.1, h=0.5);
                // Observation Port Window Trim
                part("glow", COLOR_GLOW)
                    translate([0, 0, 1.8])
                        sphere(r=0.4);
            }
        }
    }

    // ----------------------------------------------------------------
    // 3. Orbital Gantry Infrastructure (3 Pylons at 60, 180, 300 deg)
    // Positioned in the bays between the 3 colony wings
    // ----------------------------------------------------------------
    for (ang = [60, 180, 300]) {
        rotate([0, 0, ang]) {
            translate([gantry_r, 0, 0]) {
                // Tower Footing / Anchor Mount
                part("hull_dark", COLOR_HULL_DARK)
                    cylinder(r1=1.8, r2=1.3, h=1.5);

                // Angled Support Buttress from inner foundation
                part("hull_dark", COLOR_HULL_DARK) {
                    translate([-2.0, -0.6, 0])
                        cube([2.0, 1.2, 1.4]);
                }

                // Vertical Gantry Column (Open structural beam)
                part("hull_main", COLOR_HULL_MAIN) {
                    translate([-tower_w*0.5, -tower_w*0.5, 1.2])
                        cube([tower_w, tower_w, gantry_h - 1.2]);
                }

                // Inset Energy / Conduit Channel
                part("accent", COLOR_ACCENT) {
                    translate([-tower_w*0.5 - 0.15, -tower_w*0.25, 2.0])
                        cube([0.3, tower_w*0.5, gantry_h - 3.5]);
                }

                // Tower Top Beacon
                translate([0, 0, gantry_h])
                    part("glow", COLOR_GLOW)
                        sphere(r=0.55);
            }
        }
    }

    // ----------------------------------------------------------------
    // 4. Open Upper Service & Docking Collar (Hollow center!)
    // Leaves the central biosphere dome clearly visible underneath
    // ----------------------------------------------------------------
    translate([0, 0, gantry_h - 1.2]) {
        // Hollow Ring Frame
        part("hull_main", COLOR_HULL_MAIN) {
            difference() {
                cylinder(r=gantry_r + tower_w*0.6, h=1.4);
                // Large hollow aperture: dome is visible below!
                cylinder(r=gantry_r - tower_w*0.6, h=2.0, center=true);
            }
        }

        // Inner Docking Guide Clamps & Approach Lights
        for (a = [0:60:300]) {
            rotate([0, 0, a]) {
                translate([gantry_r - tower_w*0.5, 0, 0.4]) {
                    part("accent", COLOR_ACCENT)
                        cube([1.2, 1.2, 1.2], center=true);
                    part("glow", COLOR_GLOW)
                        translate([-0.6, 0, 0.5])
                            sphere(r=0.35);
                }
            }
        }

        // Refueling / Cargo Crane Arm extending over the dock
        rotate([0, 0, 120]) {
            translate([gantry_r * 0.45, 0, 0.8]) {
                part("cargo", COLOR_CARGO) {
                    // Crane beam
                    cube([gantry_r * 0.7, 1.1, 1.1], center=true);
                    // Vertical fuel hose nozzle
                    translate([-gantry_r * 0.32, 0, -1.8])
                        cylinder(r=0.6, h=2.0);
                }
            }
        }
    }

    // ----------------------------------------------------------------
    // 5. Deep Space Tracking Radar Dish (Mounted on Top Gantry)
    // ----------------------------------------------------------------
    rotate([0, 0, 60]) {
        translate([gantry_r, 0, gantry_h]) {
            // Mast
            part("hull_dark", COLOR_HULL_DARK)
                cylinder(r=0.7, h=3.0);
            // Angled Tracking Dish
            translate([0, 0, 3.0])
                rotate([25, -10, 40])
                    sensor_dish(dish_r=3.8, depth=1.3, stem_h=1.8);
        }
    }
}

// Standalone Render
spaceport_piece();
