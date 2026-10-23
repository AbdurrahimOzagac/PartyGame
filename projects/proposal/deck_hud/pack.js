// Pack rendered backgrounds + native text boxes into an editable .pptx (opens in PowerPoint and Google Slides).
// node pack.js outdir deck.pptx
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');

const [dir = 'out', OUT = 'deck.pptx'] = process.argv.slice(2);
const slides = JSON.parse(fs.readFileSync(path.join(dir, 'slides.json')));
const K = 13.333 / 1920; // px → in
const PT = 0.5;          // px → pt

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = 'Ezel Bayraktar, Abdurrahim Özağaç, Kürşat Hürkan Aydın, Ardıl Bilal Ülkü';
pres.title = 'Ice Rumble — Game Proposal';
pres.theme = { headFontFace: 'Arial Rounded MT Bold', bodyFontFace: 'Arial' };

let section = null;
slides.forEach((sd, si) => {
  if (sd.section !== section) { pres.addSection({ title: sd.section }); section = sd.section; }
  const s = pres.addSlide({ sectionTitle: sd.section });
  s.background = { data: 'image/jpeg;base64,' + fs.readFileSync(path.join(dir, sd.bg)).toString('base64') };
  sd.boxes.forEach((b, bi) => {
    // a little horizontal slack so font-metric differences never force an extra wrap
    let { x, w } = b;
    if (!b.explicitW) {
      const extra = w * 0.06 + 10;
      if (b.align === 'right') x -= extra; else if (b.align === 'center') x -= extra / 2;
      w += extra;
    } else if (b.align === 'left') w += 6;
    const runs = [];
    b.runs.forEach((r, i) => {
      if (r.br) { if (runs.length) runs[runs.length - 1].options.breakLine = true; return; }
      if (!r.text) return;
      runs.push({ text: r.text, options: { fontFace: r.font, fontSize: +(r.size * PT).toFixed(1), bold: r.bold, italic: r.italic, color: r.color, charSpacing: r.ls ? +(r.ls * PT).toFixed(2) : undefined } });
    });
    if (!runs.length) return;
    s.addText(runs, {
      x: x * K, y: b.y * K, w: w * K, h: Math.max(b.h, b.lh) * K,
      margin: 0, align: b.align, valign: b.valign, lineSpacing: +(b.lh * PT).toFixed(1),
      fit: 'none', wrap: true, isTextBox: true, objectName: `Text ${si + 1}.${bi + 1}`,
    });
  });
  if (sd.notes) s.addNotes(sd.notes);
});
pres.writeFile({ fileName: OUT }).then(() => console.log('wrote', OUT));
