"""Give pictures named '... adjN' rounded-rectangle geometry. Usage: python round.py in.pptx out.pptx"""
import re
import sys
import zipfile

SRC, DST = sys.argv[1], sys.argv[2]
PIC = re.compile(r'<p:pic>.*?</p:pic>', re.S)


def fix(m):
    pic = m.group(0)
    a = re.search(r'name="[^"]* adj(\d+)"', pic)
    if not a:
        return pic
    return re.sub(r'<a:prstGeom prst="rect">\s*<a:avLst\s*/>\s*</a:prstGeom>',
                  f'<a:prstGeom prst="roundRect"><a:avLst><a:gd name="adj" fmla="val {a.group(1)}"/></a:avLst></a:prstGeom>', pic)


with zipfile.ZipFile(SRC) as zin, zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as zout:
    n = 0
    for item in zin.infolist():
        data = zin.read(item.filename)
        if re.match(r'ppt/slides/slide\d+\.xml$', item.filename):
            text = data.decode('utf-8')
            new = PIC.sub(fix, text)
            n += text.count(' adj') and new.count('prst="roundRect"><a:avLst><a:gd')
            data = new.encode('utf-8')
        zout.writestr(item, data)
print('rounded pictures in', n, 'slide parts')
