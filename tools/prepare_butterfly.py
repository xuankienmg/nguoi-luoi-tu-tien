"""Slice generated butterfly atlas; preserve alpha and export existing pet sizes."""
from pathlib import Path
from shutil import copyfile
import sys
from PIL import Image

root=Path(__file__).resolve().parents[1]
archive=root/'docs/art/that-thai-ho-diep-atlas.png'
archive.parent.mkdir(parents=True,exist_ok=True)
copyfile(Path(sys.argv[1]),archive)
image=Image.open(archive).convert('RGBA')
assert image.size==(2172,724),image.size
for form,(left,right) in enumerate([(0,500),(500,1160),(1160,2172)]):
    crop=image.crop((left,0,right,image.height))
    crop=crop.crop(crop.getchannel('A').getbbox())
    for suffix,size in [('',(320,221)),('_hd',(240,240)),('_48',(48,48)),('_24',(24,24))]:
        width,height=size
        ratio=min((width-4)/crop.width,height*[.70,.83,.93][form]/crop.height)
        scaled=crop.resize((max(1,round(crop.width*ratio)),max(1,round(crop.height*ratio))),Image.Resampling.LANCZOS)
        out=Image.new('RGBA',size)
        out.alpha_composite(scaled,((width-scaled.width)//2,max(0,int(height*.96)-scaled.height)))
        path=root/f'wwwroot/img/pet/that_thai_ho_diep_{form}{suffix}.png'
        out.save(path,optimize=True)
        assert out.getchannel('A').getextrema()==(0,255)
        print(path.relative_to(root),size)
