import os, sys
from PIL import Image, ImageDraw
out=sys.argv[1]; names=sys.argv[2:]; tiles=[]
for n in names:
    p=f'D:/blender/propout/{n}/{n}_paint_preview.png'
    if not os.path.exists(p): print('missing',n); continue
    im=Image.open(p).convert('RGB').resize((340,340)); d=ImageDraw.Draw(im); d.rectangle((0,0,340,22),fill=(30,30,30)); d.text((6,5),n,fill=(255,255,255)); tiles.append(im)
W=min(4,len(tiles)); H=(len(tiles)+W-1)//W; s=Image.new('RGB',(340*W,340*H),(40,40,40))
for i,im in enumerate(tiles): s.paste(im,((i%W)*340,(i//W)*340))
s.save(out); print(len(tiles))
