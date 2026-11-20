// ====================================================================
// Deep Space Pioneers - Parametric Game Pieces
// Common modules and shared design language
// ====================================================================

// Rendering Quality ($fn)
$fn = $preview ? 32 : 64;

// Standard Dimensions (in mm, scaled ~1:1 with Unity 1 unit = 10mm or direct 3D print)
BASE_HEIGHT = 1.5;
SLOT_TOLERANCE = 0.25;

// Basic Colors for OpenSCAD preview
COLOR_HULL_MAIN   = [0.85, 0.88, 0.92]; // Light metallic ceramic
COLOR_HULL_DARK   = [0.25, 0.28, 0.32]; // Dark anthracite armor
COLOR_ACCENT      = [0.15, 0.60, 0.90]; // Neon cyan / Player trim
COLOR_GLOW        = [0.20, 0.95, 0.85]; // Engine & visor glow
COLOR_DOME        = [0.40, 0.75, 0.95, 0.7]; // Bio-dome glass
COLOR_SOLAR       = [0.10, 0.20, 0.45]; // Solar blue
COLOR_CARGO       = [0.90, 0.65, 0.15]; // Industrial cargo yellow

// --------------------------------------------------------------------
// Helper Modules
// --------------------------------------------------------------------

module chamfered_cylinder(r=10, h=5, c=1) {
    cylinder(r1=r-c, r2=r, h=c);
    translate([0, 0, c])
        cylinder(r=r, h=h - 2*c);
    translate([0, 0, h-c])
        cylinder(r1=r, r2=r-c, h=c);
}

module hex_prism(r=10, h=5) {
    cylinder(r=r, h=h, $fn=6);
}

module rounded_box(size=[10, 10, 5], r=1) {
    hull() {
        for (x = [r, size[0]-r]) {
            for (y = [r, size[1]-r]) {
                translate([x, y, 0])
                    cylinder(r=r, h=size[2]);
            }
        }
    }
}

// Subspace dish with central feed horn
module sensor_dish(dish_r=6, depth=2, stem_h=4) {
    color(COLOR_HULL_DARK)
        cylinder(r=1.2, h=stem_h);
    
    translate([0, 0, stem_h]) {
        color(COLOR_HULL_MAIN)
            difference() {
                sphere(r=dish_r);
                sphere(r=dish_r - 0.8);
                translate([0, 0, -dish_r])
                    cube([dish_r*2 + 2, dish_r*2 + 2, dish_r*2], center=true);
            }
        
        // Feed horn & subreflector
        color(COLOR_ACCENT) {
            cylinder(r=0.6, h=depth + 1);
            translate([0, 0, depth + 1])
                sphere(r=1.0);
        }
    }
}

// Photovoltaic solar array wing
module solar_panel(w=8, l=14, thickness=0.8) {
    color(COLOR_HULL_DARK) {
        // Frame
        cube([w, l, thickness], center=true);
        // Mount pivot
        translate([0, -l/2 - 1, 0])
            rotate([0, 90, 0])
                cylinder(r=0.9, h=w*0.6, center=true);
    }
    // Solar cell face
    color(COLOR_SOLAR)
        translate([0, 0, thickness*0.4])
            cube([w - 1.2, l - 1.5, thickness*0.5], center=true);
}

// Engine thruster nozzle with interior glow
module thruster_nozzle(r_outer=3, r_inner=2.2, len=5) {
    color(COLOR_HULL_DARK)
        difference() {
            cylinder(r1=r_outer*0.8, r2=r_outer, h=len);
            translate([0, 0, 0.8])
                cylinder(r1=r_inner*0.5, r2=r_inner, h=len);
        }
    color(COLOR_GLOW)
        translate([0, 0, 0.4])
            cylinder(r=r_inner*0.5, h=1);
}
