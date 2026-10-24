import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *
import math

# Constants
TREE_HEIGHT = 4.0
TRUNK_RADIUS = 0.3
TRUNK_SECTION_HEIGHT = 0.4
LEAF_COUNT = 6
COCONUT_RADIUS = 0.15
COCONUT_COUNT = 3

# Materials
MAT_TRUNK = mat("TrunkBrown", (0.12, 0.06, 0.03), rough=0.9)
MAT_LEAF = mat("LeafGreen", (0.08, 0.4, 0.05), rough=0.8)
MAT_LEAF2 = mat("LeafGreen2", (0.04, 0.28, 0.03), rough=0.8)
MAT_COCO = mat("CoconutBrown", (0.15, 0.08, 0.04), rough=0.8)

# 1. Trunk
num_segments = math.ceil(TREE_HEIGHT / TRUNK_SECTION_HEIGHT)
actual_section_h = TREE_HEIGHT / num_segments

for i in range(num_segments):
    z_center = (i * actual_section_h) + (actual_section_h / 2.0)
    off_x = i * 0.05
    off_y = i * 0.02
    
    r1 = TRUNK_RADIUS * (1.0 - (i / num_segments) * 0.3)
    r2 = TRUNK_RADIUS * (1.0 - ((i + 1) / num_segments) * 0.3)
    
    cyl(r1, actual_section_h, (off_x, off_y, z_center), MAT_TRUNK, axis='Z', radius2=r2, segments=12, bevel=0.02)

# Calculate trunk top position for attachments
top_x = (num_segments - 1) * 0.05
top_y = (num_segments - 1) * 0.02
top_z = TREE_HEIGHT

# 2. Leaves (Layer 1 - Large)
for i in range(LEAF_COUNT):
    a = i * (math.pi * 2.0 / LEAF_COUNT)
    # Brief instructions: center = (cx + sin(a)*0.75, cy + cos(a)*0.75, cz - 0.25)
    cx_leaf = top_x + math.sin(a) * 0.75
    cy_leaf = top_y + math.cos(a) * 0.75
    cz_leaf = top_z - 0.25
    
    # Brief instructions: rot=(-30, 0, -a)
    rot_z = -math.degrees(a)
    box((0.45, 1.6, 0.05), (cx_leaf, cy_leaf, cz_leaf), MAT_LEAF, bevel=0.01, rot=(-30, 0, rot_z))

# 2b. Leaves (Layer 2 - Smaller, shorter droop)
for i in range(LEAF_COUNT):
    # Brief instructions: heading a+30
    a_plus_30 = (i * (math.pi * 2.0 / LEAF_COUNT)) + (math.pi / 6.0)
    # Brief instructions: center = (cx + sin(a+30)*0.55, cy + cos(a+30)*0.55, cz - 0.05)
    cx_leaf2 = top_x + math.sin(a_plus_30) * 0.55
    cy_leaf2 = top_y + math.cos(a_plus_30) * 0.55
    cz_leaf2 = top_z - 0.05
    
    rot_z2 = -math.degrees(a_plus_30)
    box((0.35, 1.2, 0.05), (cx_leaf2, cy_leaf2, cz_leaf2), MAT_LEAF2, bevel=0.01, rot=(-15, 0, rot_z2))

# 3. Coconuts
# We need to ensure they are visible and not buried too deep.
for i in range(COCONUT_COUNT):
    angle = (i / COCONUT_COUNT) * math.pi * 2.0
    # Place them near the trunk top, slightly offset by radius to avoid being fully inside
    c_dist = TRUNK_RADIUS * 0.8
    cx = top_x + math.cos(angle) * c_dist
    cy = top_y + math.sin(angle) * c_dist
    cz = top_z - 0.15
    
    sphere(COCONUT_RADIUS, (cx, cy, cz), MAT_COCO, segments=12)

finish('PalmTree')