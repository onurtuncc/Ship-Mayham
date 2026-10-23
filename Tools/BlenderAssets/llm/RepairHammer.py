import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *

# Constants
HANDLE_LEN = 0.5
HANDLE_RAD = 0.025
HANDLE_LEN_HALF = HANDLE_LEN / 2.0

HEAD_WIDTH = 0.16
HEAD_HEIGHT = 0.10
HEAD_DEPTH = 0.06
HEAD_WIDTH_HALF = HEAD_WIDTH / 2.0
HEAD_HEIGHT_HALF = HEAD_HEIGHT / 2.0
HEAD_DEPTH_HALF = HEAD_DEPTH / 2.0

GRIP_COUNT = 3
GRIP_THICKNESS = 0.006
GRIP_WIDTH = 0.05

# Materials (Darker, more saturated for LINEAR color)
MAT_WOOD = mat("Wood_Dark", (0.12, 0.06, 0.03), rough=0.85)
MAT_IRON = mat("Iron_Dark", (0.04, 0.04, 0.05), rough=0.5)
MAT_RED = mat("Leather_Red", (0.3, 0.02, 0.02), rough=0.9)

# 1. Handle (Cylindrical)
handle_z = HANDLE_LEN_HALF
cyl(HANDLE_RAD, HANDLE_LEN, (0, 0, handle_z), MAT_WOOD, axis='Z', segments=12, bevel=0.01)

# 2. Grips (Thick wrap-around bands)
for i in range(GRIP_COUNT):
    # Place grips in the middle/lower section of the handle
    z_pos = 0.15 + (i * 0.1)
    cyl(HANDLE_RAD + GRIP_THICKNESS, GRIP_WIDTH, (0, 0, z_pos), MAT_RED, axis='Z', segments=12, bevel=0.01)

# 3. Hammer Head
# The head sits at the top of the handle.
head_z = HANDLE_LEN + HEAD_HEIGHT_HALF - 0.01

# Main Body Block (The iron mass)
box((HEAD_WIDTH, HEAD_DEPTH, HEAD_HEIGHT), (0, 0, head_z), MAT_IRON, bevel=0.02)

# Claw Part (The "J" shape on the +X side)
# We'll build a chunkier claw using a cylinder and a small box
# Claw base
claw_base_x = HEAD_WIDTH_HALF * 0.8
claw_base_z = head_z
box((0.04, HEAD_DEPTH, HEAD_HEIGHT * 0.8), (claw_base_x, 0, claw_base_z), MAT_IRON, bevel=0.01)

# Claw tip (angled part)
claw_tip_x = claw_base_x + 0.02
claw_tip_z = head_z + (HEAD_HEIGHT * 0.3)
box((0.04, HEAD_DEPTH * 0.6, HEAD_HEIGHT * 0.5), (claw_tip_x, 0, claw_tip_z), MAT_IRON, bevel=0.01)

# Striking Face (The flat side on the -X side)
# A slightly wider/taller protrusion on the -X side to define the face
face_x = -HEAD_WIDTH_HALF - 0.01
face_w = 0.04
face_d = HEAD_DEPTH
face_h = HEAD_HEIGHT
box((face_w, face_d, face_h), (face_x, 0, head_z), MAT_IRON, bevel=0.01)

# Eye (Transition from handle to head)
eye_z = HANDLE_LEN
cyl(HANDLE_RAD * 1.3, 0.04, (0, 0, eye_z), MAT_IRON, axis='Z', segments=12, bevel=0.01)

finish('RepairHammer')