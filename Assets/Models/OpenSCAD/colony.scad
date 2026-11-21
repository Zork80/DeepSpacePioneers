// ====================================================================
// Deep Space Pioneers - Colony Piece (Kolonie)
// Parametric planetary ground settlement (No hex plate, rests on grid)
// ====================================================================

include <common.scad>

module colony_piece(
    dome_r = 7.0,
    airlock_len = 6.5,
    airlock_w = 3.6,
    show_details = true
) {
    // 1. Central Biosphere Dome on low circular ring collar
    // Base ring collar (rests flush on ground, Z=0)
    part("hull_dark", COLOR_HULL_DARK)
        cylinder(r1=dome_r + 1.2, r2=dome_r + 0.6, h=1.2);

    // Biome Dome
    translate([0, 0, 0.8]) {
        part("dome", COLOR_DOME)
            difference() {
                sphere(r=dome_r);
                translate([0, 0, -dome_r])
                    cube([dome_r*2 + 2, dome_r*2 + 2, dome_r*2], center=true);
            }

        // Structural Reinforcement Arches (Exoskeleton Ribs)
        if (show_details) {
            part("hull_main", COLOR_HULL_MAIN) {
                for (a = [0, 60, 120]) {
                    rotate([0, 0, a])
                        difference() {
                            sphere(r=dome_r + 0.35);
                            sphere(r=dome_r);
                            translate([0, 0, -dome_r])
                                cube([dome_r*3, dome_r*3, dome_r*2], center=true);
                            cube([dome_r*3, dome_r*2 - 1.2, dome_r*3], center=true);
                        }
                }
            }

            // Top Apex Hub & Communication Spire
            translate([0, 0, dome_r - 0.4]) {
                part("hull_main", COLOR_HULL_MAIN)
                    cylinder(r1=2.0, r2=1.0, h=1.6);
                part("accent", COLOR_ACCENT) {
                    translate([0, 0, 1.6])
                        cylinder(r=0.45, h=3.2); // Beacon antenna
                    translate([0, 0, 4.8])
                        sphere(r=0.8);           // Warning beacon
                }
            }
        }
    }

    // 2. Three Radial Airlock / Habitation Outpost Wings (120° apart)
    for (ang = [0, 120, 240]) {
        rotate([0, 0, ang]) {
            translate([dome_r - 0.8, 0, 0]) {
                // Ground Corridor & Hab Pod
                part("hull_main", COLOR_HULL_MAIN) {
                    translate([0, -airlock_w/2, 0])
                        cube([airlock_len, airlock_w, 2.6]);
                    // Rounded Airlock Terminal End
                    translate([airlock_len, 0, 0])
                        cylinder(r=airlock_w/2, h=2.8);
                }

                // Outer Docking Door
                part("accent", COLOR_ACCENT)
                    translate([airlock_len + airlock_w/2 - 0.2, -airlock_w/4, 0.4])
                        cube([0.5, airlock_w/2, 1.8]);

                // Auxiliary Life-Support Coolant Tanks
                if (show_details) {
                    part("hull_dark", COLOR_HULL_DARK)
                        translate([airlock_len*0.45, airlock_w/2 + 0.8, 1.2])
                            rotate([90, 0, 0])
                                cylinder(r=1.0, h=1.6, center=true);
                }
            }
        }
    }

    // 3. High-Efficiency Solar Panel Array Wing (Angled upward)
    if (show_details) {
        rotate([0, 0, 60]) {
            translate([dome_r + 1.2, 0, 0]) {
                // Support Strut
                part("hull_dark", COLOR_HULL_DARK) {
                    cylinder(r=0.9, h=3.2);
                    translate([0, 0, 3.2])
                        rotate([0, 35, 0])
                            cylinder(r=0.6, h=2.5);
                }
                // Solar Wing
                translate([1.5, 0, 4.8])
                    rotate([0, -25, 0])
                        solar_panel(w=6.5, l=10.5, thickness=0.8);
            }
        }
    }
}

// Standalone Render
colony_piece();
