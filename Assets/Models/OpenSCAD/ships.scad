// ====================================================================
// Deep Space Pioneers - Ships (Spielfiguren Schiffe)
// Modular Deep Space Transporters: Colony Ship & Trade Ship
// Coordinates: +Y = Forward, +X = Starboard (Right), +Z = Up
// ====================================================================

include <common.scad>

// --------------------------------------------------------------------
// Base Transport Hull Chassis
// --------------------------------------------------------------------
module ship_hull_chassis(
    length = 24.0,
    width = 13.0,
    height = 5.5
) {
    // 1. Main Fuselage (Wedge Body)
    part("hull_main", COLOR_HULL_MAIN) {
        hull() {
            // Nose tip
            translate([0, length*0.5, height*0.3])
                sphere(r=1.2);
            // Mid cabin
            translate([-width*0.28, length*0.1, 0])
                cube([width*0.56, length*0.2, height*0.7]);
            // Stern bulkhead
            translate([-width*0.32, -length*0.48, 0])
                cube([width*0.64, length*0.1, height*0.8]);
        }
    }

    // 2. Cockpit Canopy & Forward Sensors
    part("glow", COLOR_GLOW) {
        translate([0, length*0.25, height*0.65]) {
            rotate([25, 0, 0])
                scale([1.0, 1.8, 0.7])
                    sphere(r=1.8);
        }
    }

    // 3. Port & Starboard Warp Outriggers / Nacelles
    for (side = [-1, 1]) {
        translate([side * width * 0.42, -length*0.05, height*0.35]) {
            // Pylon connecting wing to fuselage
            part("hull_dark", COLOR_HULL_DARK) {
                hull() {
                    translate([-side * width*0.15, -length*0.2, 0])
                        cube([width*0.15, length*0.4, 1.2]);
                    translate([0, -length*0.25, 0])
                        cylinder(r=1.4, h=2.5, center=true);
                }
            }

            // Nacelle Pod
            part("hull_main", COLOR_HULL_MAIN) {
                hull() {
                    // Forward Bussard Collector
                    translate([0, length*0.32, 0])
                        sphere(r=1.8);
                    // Mid nacelle body
                    translate([-1.6, -length*0.3, -1.5])
                        cube([3.2, length*0.55, 3.0]);
                }
            }

            // Nacelle Glowing Field Grille
            part("accent", COLOR_ACCENT) {
                translate([side * 1.5, 0, 0])
                    cube([0.5, length*0.4, 1.4], center=true);
            }

            // Front Glow Collector
            part("glow", COLOR_GLOW) {
                translate([0, length*0.33, 0])
                    sphere(r=1.1);
            }

            // Stabilizer Vertical Winglet
            part("hull_dark", COLOR_HULL_DARK) {
                translate([side * 1.6, -length*0.25, 1.2])
                    hull() {
                        cube([0.8, length*0.2, 0.5]);
                        translate([0, -length*0.05, 3.2])
                            cube([0.8, length*0.1, 0.5]);
                    }
            }
        }
    }

    // 4. Rear Main Propulsion Thrusters (Twin Nozzles)
    for (offset = [-width*0.16, width*0.16]) {
        translate([offset, -length*0.48, height*0.4]) {
            rotate([-90, 0, 0])
                thruster_nozzle(r_outer=2.0, r_inner=1.4, len=3.0);
        }
    }

    // 5. Ventral Landing / Board Stand Skids
    part("hull_dark", COLOR_HULL_DARK) {
        for (side = [-1, 1]) {
            translate([side * width*0.22, 0, -0.6])
                cube([1.2, length*0.6, 0.8], center=true);
        }
    }
}

// --------------------------------------------------------------------
// Colony Ship Payload: Settlement Dome & Seed Habitat
// --------------------------------------------------------------------
module colony_payload() {
    translate([0, -1.0, 3.8]) {
        // Retaining Cradle
        part("hull_dark", COLOR_HULL_DARK)
            cylinder(r=4.2, h=1.0);

        // Biome Settlement Module (Miniature Colony Dome)
        part("dome", COLOR_DOME)
            difference() {
                sphere(r=3.8);
                translate([0, 0, -4.0])
                    cube([8.5, 8.5, 8.0], center=true);
            }

        // Module Locking Arches
        part("accent", COLOR_ACCENT) {
            rotate([0, 0, 45])
                difference() {
                    sphere(r=4.0);
                    sphere(r=3.75);
                    translate([0, 0, -4.0])
                        cube([9.0, 9.0, 8.0], center=true);
                    cube([9.0, 6.2, 9.0], center=true);
                }
        }

        // Top Spire / Atmospheric Snorkel
        part("hull_main", COLOR_HULL_MAIN)
            translate([0, 0, 3.5])
                cylinder(r1=0.8, r2=0.4, h=1.5);
    }
}

// --------------------------------------------------------------------
// Trade Ship Payload: Modular Cargo Freight Pods
// --------------------------------------------------------------------
module trade_payload() {
    translate([0, -1.2, 3.8]) {
        // Cargo Spine / Magnetic Rail
        part("hull_dark", COLOR_HULL_DARK)
            translate([-1.0, -5.0, 0])
                cube([2.0, 10.0, 1.2]);

        // 3 Standardized Hexagonal / Rounded Freight Pods
        for (y = [-3.0, 0.2, 3.4]) {
            translate([0, y, 1.8]) {
                // Main Container Body
                part("cargo", COLOR_CARGO)
                    rotate([0, 90, 0])
                        cylinder(r=2.2, h=5.6, center=true, $fn=6);

                // Container End Caps & Magnetic Locks
                part("hull_dark", COLOR_HULL_DARK) {
                    for (side = [-2.85, 2.85]) {
                        translate([side, 0, 0])
                            rotate([0, 90, 0])
                                cylinder(r=2.0, h=0.4, center=true, $fn=6);
                    }
                }

                // Status Indicator Trim
                part("accent", COLOR_ACCENT)
                    translate([0, 0, 2.1])
                        cube([4.0, 0.8, 0.4], center=true);
            }
        }
    }
}

// --------------------------------------------------------------------
// Complete Assembled Ships
// --------------------------------------------------------------------

module colony_ship() {
    ship_hull_chassis();
    colony_payload();
}

module trade_ship() {
    ship_hull_chassis();
    trade_payload();
}

// Preview / Default Render:
// Shows both side by side when opened directly in OpenSCAD
translate([-12, 0, 0]) colony_ship();
translate([ 12, 0, 0]) trade_ship();
