import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *
import math

# Materials
foam_mat = mat('Foam', (0.8, 0.9, 0.95), rough=0.9)
foam_blue_mat = mat('FoamBlue', (0.3, 0.55, 0.7), rough=0.8)

# 1) Flat base splash
# The brief says center is (0,0,0.1) and height is 0.35 (squash Z). 
# To make it sit on the ground (z=0), the bottom should be at z=0.
# If center is at 0.1 and squash is 0.35, the bottom is at 0.1 - 0.175 = -0.075.
# We adjust the center to 0.175 so the bottom is at 0.
sphere(0.45, (0, 0, 0.175), foam_blue_mat, segments=14, squash=(1.2, 1.2, 0.35))

# 2) Main puffs
# We ensure they overlap the base. Base top is at 0.175 + 0.175 = 0.35.
# First puff center (0, 0, 0.38) with radius 0.35 means bottom is at 0.03.
# This overlaps the base nicely.
sphere(0.35, (0, 0, 0.38), foam_mat, segments=14)
sphere(0.25, (0.32, 0.05, 0.28), foam_mat, segments=14)
sphere(0.26, (-0.3, -0.05, 0.3), foam_mat, segments=14)
sphere(0.22, (0.12, 0.22, 0.58), foam_mat, segments=14)
sphere(0.2, (-0.12, -0.16, 0.6), foam_mat, segments=14)
sphere(0.17, (0.0, 0.0, 0.78), foam_mat, segments=14)

# 3) Droplets
for k in range(8):
    angle = k * math.pi / 4
    z_offset = 0.45 + 0.12 * (k % 3)
    x = 0.5 * math.cos(angle)
    y = 0.5 * math.sin(angle)
    sphere(0.06, (x, y, z_offset), foam_mat, segments=8)

for k in range(4):
    angle = k * math.pi / 2 + 0.4
    x = 0.42 * math.cos(angle)
    y = 0.42 * math.sin(angle)
    sphere(0.1, (x, y, 0.2), foam_blue_mat, segments=8)

finish('SprayPuff')