import os, sys
from PIL import Image, ImageDraw
names=sys.argv[2:]; out=sys.argv[1]
tiles=[]
for n in names:
    p=f'D:/blender/paint/{n}/{n}_paint_preview.png'
    if not os.path.exists(p): continue
    im=Image.open(p).convert('RGB').resize((320,320))
    d=ImageDraw.Draw(im); d.rectangle((0,0,320,22),fill=(30,30,30)); d.text((6,5),n,fill=(255,255,255)); tiles.append(im)
W=5; H=(len(tiles)+W-1)//W
s=Image.new('RGB',(320*W,320*H),(40,40,40))
for i,im in enumerate(tiles): s.paste(im,((i%W)*320,(i//W)*320))
s.save(out); print(len(tiles))
