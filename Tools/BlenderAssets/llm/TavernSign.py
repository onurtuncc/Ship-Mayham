import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *

# Constants for layout
POST_X = -0.75
POST_Z_TOP = 1.6
ARM_Z = 1.58
ARM_LEN = 1.5

CHAIN_H = 0.34
CHAIN_Z_BOTTOM = 1.25 # Board top
CHAIN_Z_CENTER = CHAIN_H / 2 + CHAIN_Z_BOTTOM # 1.42
CHAIN_X_L = -0.45
CHAIN_X_R = 0.45

BOARD_W = 1.2
BOARD_H = 0.6
BOARD_D = 0.08
BOARD_Z = 0.95

# Gull specific constants
GULL_Y_FRONT = -0.075
GULL_Y_BACK = 0.075
GULL_THICK = 0.04

# Colors (Linear/Dark Saturated)
COL_WOOD = (0.12, 0.055, 0.02)
COL_IRON = (0.03, 0.03, 0.035)
COL_BLUE = (0.02, 0.06, 0.15)
COL_WHITE = (0.8, 0.8, 0.8)
COL_ORANGE = (0.7, 0.25, 0.02)

# Register Materials
mat_wood = mat('Wood', COL_WOOD)
mat_iron = mat('Iron', COL_IRON)
mat_blue = mat('Blue', COL_BLUE)
mat_white = mat('White', COL_WHITE)
mat_orange = mat('Orange', COL_ORANGE)

# 1. Wall post and base
cyl(0.06, 1.6, (POST_X, 0, 0.8), mat_iron, axis='Z')
box((0.3, 0.3, 0.1), (POST_X, 0, 0.05), mat_iron, bevel=0.015)

# 2. Horizontal arm
cyl(0.045, 1.5, (0, 0, ARM_Z), mat_iron, axis='X')

# 3. Two chains
box((0.03, 0.03, CHAIN_H), (CHAIN_X_L, 0, CHAIN_Z_CENTER), mat_iron, bevel=0.005)
box((0.03, 0.03, CHAIN_H), (CHAIN_X_R, 0, CHAIN_Z_CENTER), mat_iron, bevel=0.005)

# 4. Board frame
box((BOARD_W, BOARD_D, BOARD_H), (0, 0, BOARD_Z), mat_wood, bevel=0.015)

# 5. Blue panel on both faces
box((1.08, 0.09, 0.48), (0, -0.04, BOARD_Z), mat_blue, bevel=0.01)
box((1.08, 0.09, 0.48), (0, 0.04, BOARD_Z), mat_blue, bevel=0.01)

# Helper function for a cleaner seagull shape
def draw_gull(y_pos):
    # Body: slightly tapered ellipsoid-like box
    box((0.25, GULL_THICK, 0.12), (0, y_pos, 0.93), mat_white, bevel=0.01)
    
    # Wings: angled upward and outward
    # Left wing
    box((0.35, GULL_THICK, 0.06), (-0.2, y_pos, 0.98), mat_white, rot=(0, -35, 0), bevel=0.005)
    # Right wing
    box((0.35, GULL_THICK, 0.06), (0.2, y_pos, 0.98), mat_white, rot=(0, 35, 0), bevel=0.005)
    
    # Head: small sphere
    sphere(0.055, (0.18, y_pos, 1.0), mat_white, segments=12)
    
    # Beak: small orange cone-like box
    box((0.06, GULL_THICK, 0.02), (0.25, y_pos, 1.0), mat_orange, bevel=0.001)

# 6. White seagull on the FRONT and BACK
draw_gull(GULL_Y_FRONT)
draw_gull(GULL_Y_BACK)

finish('TavernSign')