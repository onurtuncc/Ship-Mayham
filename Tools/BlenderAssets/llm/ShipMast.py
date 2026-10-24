import sys, importlib; sys.path.insert(0, 'D:/blender'); import llm_helpers; importlib.reload(llm_helpers); from llm_helpers import *

# Constants for dimensions and positions as per reviewer feedback
# Base block: box((0.7,0.7,0.2),(0,0,0.1),'Wood_Dark',bevel=0.02)
# Mast pole: cyl(0.14, 4.2, (0,0,2.1),'Wood',axis='Z',segments=12)
# Yard-arm: cyl(0.07, 2.8, (0,0,3.4),'Wood_Dark',axis='X')
# Sail: box((2.4,0.06,1.9),(0,0.16,2.4),'Sail',bevel=0.01)
# Red stripe: box((2.42,0.075,0.3),(0,0.16,2.4),'Red')
# Crow's nest: cyl(0.45,0.4,(0,0,3.95),'Wood_Dark',axis='Z',segments=12,bevel=0.02)
# Nest top: cyl(0.38,0.04,(0,0,4.15),'Wood',axis='Z')
# Rope ties:
#   (-1.3, 0.0, 3.3)
#   (1.3, 0.0, 3.3)
#   (-1.15, 0.16, 1.5)
#   (1.15, 0.16, 1.5)

def build():
    # 1. Register Materials
    # Wood (0.12,0.055,0.02)
    mat_wood = mat('Wood', (0.12, 0.055, 0.02), 0.8)
    # Wood_Dark (0.07,0.03,0.012)
    mat_wood_dark = mat('Wood_Dark', (0.07, 0.03, 0.012), 0.8)
    # Sail (0.8,0.65,0.4) rough 0.9
    mat_sail = mat('Sail', (0.8, 0.65, 0.4), 0.9)
    # Red (0.5,0.02,0.015)
    mat_red = mat('Red', (0.5, 0.02, 0.015), 0.9)
    # Rope (0.4,0.3,0.15)
    mat_rope = mat('Rope', (0.4, 0.3, 0.15), 0.9)

    # 2. Build Geometry
    # Base block
    box((0.7, 0.7, 0.2), (0, 0, 0.1), 'Wood_Dark', bevel=0.02)
    
    # Mast pole
    cyl(0.14, 4.2, (0, 0, 2.1), 'Wood', axis='Z', segments=12)
    
    # Yard-arm
    cyl(0.07, 2.8, (0, 0, 3.4), 'Wood_Dark', axis='X', segments=12)
    
    # Sail (hangs below the yard)
    # Top edge at 3.4 - (1.9/2) = 2.45? No, the instruction says center is (0, 0.16, 2.4)
    # If center is 2.4 and height is 1.9, top is 2.4 + 0.95 = 3.35. 
    # Yard is at 3.4. 3.4 - 3.35 = 0.05 overlap. Good.
    box((2.4, 0.06, 1.9), (0, 0.16, 2.4), 'Sail', bevel=0.01)
    
    # Red stripe
    # Center (0, 0.16, 2.4), height 0.3.
    box((2.42, 0.075, 0.3), (0, 0.16, 2.4), 'Red')
    
    # Crow's nest
    cyl(0.45, 0.4, (0, 0, 3.95), 'Wood_Dark', axis='Z', segments=12, bevel=0.02)
    cyl(0.38, 0.04, (0, 0, 4.15), 'Wood', axis='Z', segments=12)
    
    # Rope ties
    box((0.06, 0.06, 0.2), (-1.3, 0.0, 3.3), 'Rope')
    box((0.06, 0.06, 0.2), (1.3, 0.0, 3.3), 'Rope')
    box((0.06, 0.06, 0.2), (-1.15, 0.16, 1.5), 'Rope')
    box((0.06, 0.06, 0.2), (1.15, 0.16, 1.5), 'Rope')

    # 3. Finalize
    finish('ShipMast')

build()