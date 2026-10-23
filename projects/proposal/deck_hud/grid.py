import sys
from PIL import Image
fs=sys.argv[2:]; ims=[Image.open(f) for f in fs]; w,h=ims[0].size; cols=2
g=Image.new('RGB',(w*cols,h*((len(ims)+cols-1)//cols)),'white')
for i,im in enumerate(ims): g.paste(im,((i%cols)*w,(i//cols)*h))
g.save(sys.argv[1])
