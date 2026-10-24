import os, sys
from PIL import Image, ImageDraw
names=sys.argv[1:]
tiles=[]
for n in names:
    p=f'D:/blender/paint/{n}/{n}_paint_preview.png'
    if not os.path.exists(p): continue
    im=Image.open(p).convert('RGB').crop((100,100,668,668)).resize((384,384))
    d=ImageDraw.Draw(im); d.rectangle((0,0,384,24),fill=(30,30,30)); d.text((8,6),n+' (painted)',fill=(255,255,255)); tiles.append(im)
W=4; H=(len(tiles)+W-1)//W
s=Image.new('RGB',(384*W,384*H),(40,40,40))
for i,im in enumerate(tiles): s.paste(im,((i%W)*384,(i//W)*384))
s.save('D:/blender/paint/paint_sheet.png'); print(len(tiles))
