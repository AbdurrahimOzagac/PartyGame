// node pack_flat.js out_flat deck.pptx — one full-slide image per slide, speaker notes kept
const fs = require('fs'), path = require('path'), pptxgen = require('pptxgenjs');
const [dir = 'out_flat', OUT = 'deck.pptx'] = process.argv.slice(2);
const slides = JSON.parse(fs.readFileSync(path.join(dir, 'slides.json')));
const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = 'Ezel Bayraktar, Abdurrahim Özağaç, Kürşat Hürkan Aydın, Ardıl Bilal Ülkü';
pres.title = 'Ice Rumble — Game Proposal';
let section = null;
slides.forEach((sd, i) => {
  if (sd.section !== section) { pres.addSection({ title: sd.section }); section = sd.section; }
  const s = pres.addSlide({ sectionTitle: sd.section });
  s.addImage({ path: path.join(dir, sd.img), x: 0, y: 0, w: 13.333, h: 7.5, altText: `Slide ${i + 1}`, objectName: `Slide ${i + 1}` });
  if (sd.notes) s.addNotes(sd.notes);
});
pres.writeFile({ fileName: OUT }).then(() => console.log('wrote', OUT));
