"""Add a fade transition to every slide and auto-play fade-in entrances.

Shapes whose name starts with "g<N>|" fade in as group N, in order, each group
starting after the previous one (After Previous), so a slide builds itself on entry.
Usage: python animate.py deck.pptx out.pptx
"""
import re
import sys
import zipfile

SRC, DST = sys.argv[1], sys.argv[2]
DUR, GAP = 350, 120  # ms per fade, ms between groups
SHAPE = re.compile(r'<p:(sp|pic|graphicFrame)>\s*<p:nv\w+Pr>\s*<p:cNvPr id="(\d+)" name="g(\d+)\|')


def effect(ids, spid, kind, node_type):
    a, b, c = ids
    return (f'<p:par><p:cTn id="{a}" presetID="10" presetClass="entr" presetSubtype="0" fill="hold" grpId="0" nodeType="{node_type}">'
            f'<p:stCondLst><p:cond delay="0"/></p:stCondLst><p:childTnLst>'
            f'<p:set><p:cBhvr><p:cTn id="{b}" dur="1" fill="hold"><p:stCondLst><p:cond delay="0"/></p:stCondLst></p:cTn>'
            f'<p:tgtEl><p:spTgt spid="{spid}"/></p:tgtEl><p:attrNameLst><p:attrName>style.visibility</p:attrName></p:attrNameLst></p:cBhvr>'
            f'<p:to><p:strVal val="visible"/></p:to></p:set>'
            f'<p:animEffect transition="in" filter="fade"><p:cBhvr><p:cTn id="{c}" dur="{DUR}"/><p:tgtEl><p:spTgt spid="{spid}"/></p:tgtEl></p:cBhvr></p:animEffect>'
            f'</p:childTnLst></p:cTn></p:par>')


def timing(groups):
    nid = [3]

    def take(n=1):
        out = list(range(nid[0] + 1, nid[0] + 1 + n))
        nid[0] += n
        return out

    pars, builds, t = [], [], 0
    for gi, (g, shapes) in enumerate(sorted(groups.items())):
        (gid,) = take()
        effs = []
        for j, (kind, spid) in enumerate(shapes):
            effs.append(effect(take(3), spid, kind, 'afterEffect' if j == 0 else 'withEffect'))
            if kind == 'graphicFrame':
                builds.append(f'<p:bldGraphic spid="{spid}" grpId="0"><p:bldAsOne/></p:bldGraphic>')
            elif kind == 'sp':
                builds.append(f'<p:bldP spid="{spid}" grpId="0" animBg="1"/>')
        pars.append(f'<p:par><p:cTn id="{gid}" fill="hold"><p:stCondLst><p:cond delay="{t}"/></p:stCondLst><p:childTnLst>{"".join(effs)}</p:childTnLst></p:cTn></p:par>')
        t += DUR + GAP
    return ('<p:timing><p:tnLst><p:par><p:cTn id="1" dur="indefinite" restart="never" nodeType="tmRoot"><p:childTnLst>'
            '<p:seq concurrent="1" nextAc="seek"><p:cTn id="2" dur="indefinite" nodeType="mainSeq"><p:childTnLst>'
            '<p:par><p:cTn id="3" fill="hold"><p:stCondLst><p:cond delay="indefinite"/><p:cond evt="onBegin" delay="0"><p:tn val="2"/></p:cond></p:stCondLst>'
            f'<p:childTnLst>{"".join(pars)}</p:childTnLst></p:cTn></p:par>'
            '</p:childTnLst></p:cTn><p:prevCondLst><p:cond evt="onPrev" delay="0"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:prevCondLst>'
            '<p:nextCondLst><p:cond evt="onNext" delay="0"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:nextCondLst></p:seq>'
            f'</p:childTnLst></p:cTn></p:par></p:tnLst><p:bldLst>{"".join(builds)}</p:bldLst></p:timing>')


with zipfile.ZipFile(SRC) as zin, zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as zout:
    for item in zin.infolist():
        data = zin.read(item.filename)
        if re.fullmatch(r'ppt/slides/slide\d+\.xml', item.filename):
            xml = data.decode('utf-8')
            groups = {}
            for kind, spid, g in SHAPE.findall(xml):
                groups.setdefault(int(g), []).append((kind, spid))
            extra = '<p:transition spd="med"><p:fade/></p:transition>'
            if groups:
                extra += timing(groups)
            xml = xml.replace('</p:clrMapOvr>', '</p:clrMapOvr>' + extra, 1)
            data = xml.encode('utf-8')
        zout.writestr(item, data)
print('animated', DST)
