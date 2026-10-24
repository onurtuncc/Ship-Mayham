import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *

# Constants for precise placement
# Wheels
R_WHEEL = 0.16
Z_WHEEL = R_WHEEL
X_WHEEL = 0.26
Y_WHEEL_FRONT = 0.25
Y_WHEEL_BACK = -0.25

# Carriage
W_RAIL = 0.08
H_RAIL = 0.12
L_RAIL = 0.7
X_RAIL = 0.2
Z_RAIL_CENTER = 0.26 # rail top is 0.32

W_CROSS = 0.4
H_CROSS = 0.1
D_CROSS = 0.08
Y_CROSS_FRONT = 0.25
Y_CROSS_BACK = -0.25
Z_CROSS_CENTER = 0.26

# Barrel (Tapered Cylinder)
B_RADIUS_BREECH = 0.1
B_RADIUS_MUZZLE = 0.075
B_LENGTH = 1.0
B_CENTER_X = 0.0
B_CENTER_Y = 0.1
B_CENTER_Z = 0.42 # Barrel bottom is 0.42 - 0.1 = 0.32 (matches rail top)

# Bands (Rings)
BAND_Y_POS = [-0.3, 0.1, 0.45, 0.6]
BAND_DEPTH = 0.06
BAND_GAP = 0.012

# Cascabel
C_RADIUS = 0.07
C_Y = -0.45 
C_Z = 0.42

# Trunnions
T_W = 0.06
T_H = 0.1
T_D = 0.06
T_X = 0.14
T_Y = 0.1
T_Z = 0.36

def run():
    # Materials
    mat_iron = mat("Mat_Iron", (0.03, 0.03, 0.035), 0.5)
    mat_brass = mat("Mat_Brass", (0.55, 0.35, 0.04), 0.3)
    mat_wood = mat("Mat_Wood", (0.12, 0.055, 0.02), 0.8)
    mat_rim = mat("Mat_Rim", (0.02, 0.02, 0.02), 0.6)

    # 1. Carriage Side Rails
    for x in [-X_RAIL, X_RAIL]:
        box((W_RAIL, L_RAIL, H_RAIL), (x, 0, Z_RAIL_CENTER), mat_wood, bevel=0.02)

    # 2. Carriage Cross Blocks
    for y in [Y_CROSS_FRONT, Y_CROSS_BACK]:
        box((W_CROSS, D_CROSS, H_CROSS), (0, y, Z_CROSS_CENTER), mat_wood, bevel=0.02)

    # 3. Wheels
    for wx, wy in [(-X_WHEEL, Y_WHEEL_BACK), (X_WHEEL, Y_WHEEL_BACK), 
                   (-X_WHEEL, Y_WHEEL_FRONT), (X_WHEEL, Y_WHEEL_FRONT)]:
        cyl(R_WHEEL, 0.08, (wx, wy, Z_WHEEL), mat_wood, axis='X', bevel=0.02)
        cyl(R_WHEEL + 0.015, 0.09, (wx, wy, Z_WHEEL), mat_rim, axis='X', bevel=0.02)
        hub_x = wx + (0.04 if wx < 0 else -0.04)
        cyl(0.04, 0.02, (hub_x, wy, Z_WHEEL), mat_brass, axis='Z', bevel=0.01)

    # 4. Barrel
    cyl(B_RADIUS_BREECH, B_LENGTH, (B_CENTER_X, B_CENTER_Y, B_CENTER_Z), mat_iron, axis='Y', radius2=B_RADIUS_MUZZLE, segments=12, bevel=0.02)

    # 5. Cascabel
    sphere(C_RADIUS, (0, C_Y, B_CENTER_Z), mat_iron, segments=12)

    # 6. Trunnion Blocks
    for tx in [-T_X, T_X]:
        box((T_W, T_D, T_H), (tx, T_Y, T_Z), mat_iron, bevel=0.01)

    # 7. Brass Bands
    for by in BAND_Y_POS:
        r_at_y = 0.1 + (by + 0.4) * (-0.025)
        band_r = r_at_y + BAND_GAP
        cyl(band_r, BAND_DEPTH, (0, by, B_CENTER_Z), mat_brass, axis='Y', segments=12, bevel=0.01)

    finish('Cannon')

run()