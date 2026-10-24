import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *
import math

# Materials
m_wood = mat('Wood', (0.2, 0.09, 0.035), rough=0.8)
m_wood_dark = mat('Wood_Dark', (0.12, 0.055, 0.02), rough=0.8)
m_iron = mat('Iron', (0.03, 0.03, 0.035), rough=0.5)
m_rope = mat('Rope', (0.5, 0.4, 0.25), rough=0.9)

# 1) 4 corner posts
for sx in [-1, 1]:
    for sy in [-1, 1]:
        # sx*0.45, sy*0.45, 0.5. Height is 1.0, so center z=0.5.
        # To have top at z=1.0, center must be at z=0.5.
        box((0.1, 0.1, 1.0), (sx * 0.45, sy * 0.45, 0.5), m_wood_dark, bevel=0.012)

# 2) Wall planks, 5 rows
for i in range(5):
    z_layer = 0.1 + 0.2 * i
    # Front/Back
    for sy in [-1, 1]:
        box((0.8, 0.04, 0.18), (0, sy * 0.48, z_layer), m_wood, bevel=0.008)
    # Left/Right
    for sx in [-1, 1]:
        box((0.04, 0.8, 0.18), (sx * 0.48, 0, z_layer), m_wood, bevel=0.008)

# 3) Top planks
for x in [-0.31, 0, 0.31]:
    # Top at z=1.025. Height 0.05 -> center z = 1.025 - 0.025 = 1.0.
    # Wait, prompt says "Top: 3 planks box((0.3,0.96,0.05),(x,0,1.025)..."
    # If center is 1.025, top is 1.05.
    box((0.3, 0.96, 0.05), (x, 0, 1.025), m_wood, bevel=0.008)

# 4) Two iron bands
for z_band in [0.3, 0.7]:
    # Front/Back bands
    for sy in [-1, 1]:
        box((1.06, 0.03, 0.14), (0, sy * 0.505, z_band), m_iron)
    # Left/Right bands
    for sx in [-1, 1]:
        box((0.03, 1.06, 0.14), (sx * 0.505, 0, z_band), m_iron)

# 5) Broken plank
# sy=-1 is front. z=0.5.
box((0.5, 0.04, 0.18), (0.1, -0.56, 0.5), m_wood, rot=(0, 0, 14))

# 6) Rope loop on the right side (sx=1)
for k in range(8):
    angle = k * math.pi / 4
    rx = 0.53
    ry = 0.12 * math.cos(angle)
    rz = 0.55 + 0.12 * math.sin(angle)
    box((0.07, 0.07, 0.07), (rx, ry, rz), m_rope)

finish('FloatingCrate')