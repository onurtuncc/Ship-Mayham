import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *
import math

# Constants
W = 0.9
D = 0.55
H = 0.4

# Materials
mat_wood_dark = mat('Wood_Dark', (0.12, 0.055, 0.02), rough=0.8)
mat_wood = mat('Wood', (0.2, 0.09, 0.035), rough=0.8)
mat_brass = mat('Brass', (0.55, 0.35, 0.04), rough=0.4)
mat_gold = mat('Gold', (0.7, 0.5, 0.02), rough=0.3)
mat_gem = mat('Gem', (0.6, 0.01, 0.02), rough=0.2)
mat_iron = mat('Iron', (0.03, 0.03, 0.035), rough=0.8)

# 1) Body
box((W, D, H), (0, 0, H/2), mat_wood_dark, bevel=0.012)

# 2) Lid
# Instruction: "use center (0, 0.05, H+0.06) with rot=(-12,0,0)"
halfcyl(D/2, W, (0, 0.05, H+0.06), mat_wood, segments=14, bevel=0.01, rot=(-12, 0, 0))

# 3) Brass bands
for bx in [-0.3, 0, 0.3]:
    box((0.07, D + 0.02, H + 0.02), (bx, 0, H/2), mat_brass, bevel=0.008)

# 4) Corner plates
for sx in [-1, 1]:
    for sy in [-1, 1]:
        box((0.09, 0.09, 0.14), (sx * W/2, sy * D/2, H/2), mat_brass, bevel=0.008)

# 5) Lock
# Front of chest is -Y. Lock should be at y = -D/2
box((0.1, 0.04, 0.12), (0, -D/2 - 0.01, H * 0.5), mat_brass, bevel=0.008)

# 6) Coins
for i in range(20):
    angle = i * 1.3
    rx = math.sin(angle) * 0.25
    ry = math.cos(angle) * 0.12 - 0.05
    rz = H + 0.02 + (math.sin(angle * 2) * 0.03)
    cyl(0.04, 0.015, (rx, ry, rz), mat_gold, axis='Z', segments=8)

# Gem
sphere(0.045, (0.1, -0.05, H + 0.08), mat_gem, segments=8, squash=(1, 1, 1.2))

finish('TreasureChest')