"""Slice generated three-form thunder dragon atlas and preserve alpha."""
from pathlib import Path
from shutil import copyfile
import sys
from PIL import Image
root=Path(__file__).resolve().parents[1]
archive=root/'docs/art/thai-co-loi-long-evolution.png'
copyfile(Path(sys.argv[1]),archive)
image=Image.open(archive).convert('RGBA')
assert image.size==(2172,724)
for form,(left,right) in enumerate([(0,550),(550,1270),(1270,2172)]):
    crop=image.crop((left,0,right,image.height));crop=crop.crop(crop.getchannel('A').getbbox())
    for suffix,size in [('',(320,221)),('_hd',(240,240)),('_48',(48,48)),('_24',(24,24))]:
        width,height=size;ratio=min((width-4)/crop.width,height*[.68,.82,.94][form]/crop.height)
        resized=crop.resize((max(1,round(crop.width*ratio)),max(1,round(crop.height*ratio))),Image.Resampling.LANCZOS)
        out=Image.new('RGBA',size);out.alpha_composite(resized,((width-resized.width)//2,max(0,int(height*.96)-resized.height)))
        target=root/f'wwwroot/img/pet/thai_co_loi_long_{form}{suffix}.png';out.save(target,optimize=True)
        assert out.size==size and out.getchannel('A').getextrema()[0]==0
print('PASS 12 transparent thunder dragon assets, 3 forms and 4 sizes')
