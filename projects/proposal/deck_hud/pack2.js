// Pack render2 output into an editable .pptx: native shapes, pictures and text boxes in paint order.
// node pack2.js out2 deck.pptx
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');

const [dir = 'out2', OUT = 'deck.pptx'] = process.argv.slice(2);
const slides = JSON.parse(fs.readFileSync(path.join(dir, 'slides.json')));
const K = 13.333 / 1920; // px → in
const PT = 0.5; // px → pt (1920px = 13.333in = 960pt)

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = 'Ezel Bayraktar, Abdurrahim Özağaç, Kürşat Hürkan Aydın, Ardıl Bilal Ülkü';
pres.title = 'Shooter Party — Game Proposal';
pres.theme = { headFontFace: 'Arial Rounded MT Bold', bodyFontFace: 'Arial' };

let section = null;
slides.forEach((sd, si) => {
  if (sd.section !== section) { pres.addSection({ title: sd.section }); section = sd.section; }
  const s = pres.addSlide({ sectionTitle: sd.section });
  s.background = { path: path.join(dir, sd.bg) };
  let n = 0;
  for (const it of sd.items) {
    const name = `${it.kind} ${si + 1}.${++n}`;
    if (it.kind === 'shape') {
      const r = it.r * K;
      const o = {
        x: it.x * K, y: it.y * K, w: it.w * K, h: it.h * K, objectName: name,
        fill: it.fill ? { color: it.fill.hex, transparency: Math.round((1 - it.fill.a) * 100) } : { type: 'none' },
        line: it.line ? { color: it.line.hex, width: +(it.line.w * PT).toFixed(2), dashType: it.line.dash ? 'dash' : 'solid' } : { type: 'none' },
      };
      if (it.shadow) o.shadow = { type: 'outer', angle: 90, blur: 16, offset: 6, color: '1E468C', opacity: 0.16 };
      if (r > 0.004) { o.rectRadius = Math.min(r, Math.min(o.w, o.h) / 2); s.addShape(pres.ShapeType.roundRect, o); }
      else s.addShape(pres.ShapeType.rect, o);
    } else if (it.kind === 'vline') {
      s.addShape(pres.ShapeType.line, { x: it.x * K, y: it.y * K, w: 0, h: it.h * K, line: { color: it.hex, width: +(it.wpx * PT).toFixed(2), dashType: 'dash' }, objectName: name });
    } else if (it.kind === 'img' || it.kind === 'raster') {
      // round.py turns pictures named "... adjN" into rounded-rectangle geometry
      const adj = it.kind === 'img' && it.r > 0 ? Math.round(Math.min(it.r / Math.min(it.w, it.h), 0.5) * 100000) : 0;
      s.addImage({ path: path.join(dir, it.file), x: it.x * K, y: it.y * K, w: it.w * K, h: it.h * K, objectName: adj ? `${name} adj${adj}` : name, altText: it.kind === 'img' ? path.basename(it.src) : 'diagram' });
    } else if (it.kind === 'text') {
      let { x, w } = it;
      if (!it.explicitW) { // slack so font-metric differences never force an extra wrap
        const extra = w * 0.06 + 10;
        if (it.align === 'right') x -= extra; else if (it.align === 'center') x -= extra / 2;
        w += extra;
      } else if (it.align === 'left') w += 6;
      const runs = [];
      it.runs.forEach(r => {
        if (r.br) { if (runs.length) runs[runs.length - 1].options.breakLine = true; return; }
        if (!r.text) return;
        runs.push({ text: r.text, options: { fontFace: r.font, fontSize: +(r.size * PT).toFixed(1), bold: r.bold, italic: r.italic, color: r.color, charSpacing: r.ls ? +(r.ls * PT).toFixed(2) : undefined } });
      });
      if (!runs.length) continue;
      s.addText(runs, {
        x: x * K, y: it.y * K, w: w * K, h: Math.max(it.h, it.lh) * K, margin: 0, align: it.align, valign: it.valign,
        lineSpacing: +(it.lh * PT).toFixed(1), fit: 'none', wrap: true, isTextBox: true, objectName: name,
      });
    }
  }
  if (sd.notes) s.addNotes(sd.notes);
});
pres.writeFile({ fileName: OUT }).then(() => console.log('wrote', OUT));
