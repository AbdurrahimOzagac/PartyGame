// Decompose each HTML slide into native parts for an editable .pptx:
// shapes (cards, chips, bars, dots, lines), pictures (photos, cropped at source resolution),
// transparent rasters (SVG diagrams/icons, gradient overlays) and text boxes, in paint order.
// Only the plain page gradient becomes the slide background.
// node render2.mjs slides.html out2
import fs from 'node:fs';
import path from 'node:path';
import puppeteer from 'puppeteer-core';
import sharp from 'sharp';

const [file = 'slides.html', outDir = 'out2'] = process.argv.slice(2);
const root = path.dirname(new URL(import.meta.url).pathname);
const out = path.join(root, outDir);
fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(out, { recursive: true });
const SCALE = 2.5; // raster density for SVGs and overlays

const browser = await puppeteer.launch({ executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true, args: ['--allow-file-access-from-files'] });
const page = await browser.newPage();
page.on('pageerror', e => console.error('[pageerror]', e.message));
await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: SCALE });
await page.goto('file://' + path.join(root, file), { waitUntil: 'networkidle0' });
await page.waitForFunction('window.READY === true');
await page.evaluate(() => document.fonts.ready);

const slides = await page.evaluate(() => {
  const rgba = c => { const m = c && c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const [r, g, b, a = 1] = m[1].split(',').map(Number); return { hex: [r, g, b].map(v => Math.round(v).toString(16).padStart(2, '0')).join('').toUpperCase(), a }; };
  const isInline = el => getComputedStyle(el).display === 'inline';
  const zOf = (el, sec) => { for (let e = el; e && e !== sec; e = e.parentElement) { const cs = getComputedStyle(e); if (cs.position !== 'static' && cs.zIndex !== 'auto') return +cs.zIndex; } return 0; };
  const radius = (cs, w, h) => { const v = cs.borderTopLeftRadius; const r = v.endsWith('%') ? Math.min(w, h) * parseFloat(v) / 100 : parseFloat(v) || 0; return Math.min(r, Math.min(w, h) / 2); };
  let rid = 0;
  const res = [];
  document.querySelectorAll('section.slide').forEach((sec, si) => {
    const sr = sec.getBoundingClientRect();
    const items = [];
    let order = 0;
    const rect = el => { const r = el.getBoundingClientRect(); return { x: r.left - sr.left, y: r.top - sr.top, w: r.width, h: r.height }; };
    const push = (el, it) => items.push({ ...it, z: zOf(el, sec), o: order++ });
    const raster = (el, box) => { const id = 'r' + (rid++); el.dataset.rid = id; return id; };
    const walk = el => {
      for (const c of el.children) {
        if (c.tagName === 'ASIDE') continue;
        const cs = getComputedStyle(c);
        if (cs.display === 'none') continue;
        if (c.tagName.toLowerCase() === 'svg') {
          let b;
          if (c.classList.contains('ov')) { const g = c.getBBox(); const pad = 8; b = { x: g.x - pad + (c.getBoundingClientRect().left - sr.left), y: g.y - pad + (c.getBoundingClientRect().top - sr.top), w: g.width + 2 * pad, h: g.height + 2 * pad }; }
          else b = rect(c);
          if (b.w > 0 && b.h > 0) push(c, { kind: 'raster', id: raster(c), ...b });
          continue;
        }
        if (c.tagName === 'IMG') {
          const b = rect(c);
          let r = radius(cs, b.w, b.h);
          const p = c.parentElement, pcs = getComputedStyle(p);
          if (pcs.overflow === 'hidden') r = Math.max(r, radius(pcs, p.getBoundingClientRect().width, p.getBoundingClientRect().height) - parseFloat(pcs.borderTopWidth || 0));
          push(c, { kind: 'img', src: c.getAttribute('src'), nw: c.naturalWidth, nh: c.naturalHeight, fit: cs.objectFit, pos: cs.objectPosition, r, ...b });
          continue;
        }
        const b = rect(c);
        const bgImg = cs.backgroundImage && cs.backgroundImage !== 'none';
        const fill = rgba(cs.backgroundColor);
        const ins = cs.boxShadow.match(/(rgba?\([^)]+\))\s+0px\s+0px\s+0px\s+([\d.]+)px\s+inset/) || cs.boxShadow.match(/inset\s+0px\s+0px\s+0px\s+([\d.]+)px\s+(rgba?\([^)]+\))/);
        const soft = /\b\d+px\s+(2[0-9]|[3-9]\d)px/.test(cs.boxShadow.replace(/inset[^,]*/g, ''));
        if (bgImg && c !== sec) {
          push(c, { kind: 'raster', id: raster(c), bgOnly: true, ...b });
        } else if ((fill && fill.a > 0.01) || ins) {
          let line = null;
          if (ins) { const col = rgba(ins[1].startsWith('rgb') ? ins[1] : ins[2]); const wpx = parseFloat(ins[1].startsWith('rgb') ? ins[2] : ins[1]); line = { hex: col.hex, w: wpx }; }
          if (cs.borderTopWidth && parseFloat(cs.borderTopWidth) > 0 && cs.borderTopStyle === 'solid') { const bc = rgba(cs.borderTopColor); if (bc && bc.a > 0) { line = { hex: bc.hex, w: parseFloat(cs.borderTopWidth), outer: true }; } }
          if (cs.borderTopStyle === 'dashed') { const bc = rgba(cs.borderTopColor); line = { hex: bc.hex, w: parseFloat(cs.borderTopWidth), dash: true }; }
          push(c, { kind: 'shape', fill: fill && fill.a > 0.01 ? fill : null, line, r: radius(cs, b.w, b.h), shadow: soft, ...b });
        } else if (cs.borderTopStyle === 'dashed' && parseFloat(cs.borderTopWidth) > 0) {
          const bc = rgba(cs.borderTopColor);
          push(c, { kind: 'shape', fill: null, line: { hex: bc.hex, w: parseFloat(cs.borderTopWidth), dash: true }, r: radius(cs, b.w, b.h), shadow: false, ...b });
        } else if (cs.borderLeftStyle === 'dashed' && parseFloat(cs.borderLeftWidth) > 0 && b.w < 3) {
          const bc = rgba(cs.borderLeftColor);
          push(c, { kind: 'vline', hex: bc.hex, wpx: parseFloat(cs.borderLeftWidth), ...b });
        }
        // text owned by this element (directly or through inline children)
        const own = [...c.childNodes].some(n => (n.nodeType === 3 && n.textContent.trim()) || (n.nodeType === 1 && isInline(n) && n.textContent.trim() && n.tagName.toLowerCase() !== 'svg'));
        if (own && !isInline(c)) {
          const runs = [];
          const w2 = (node, styleEl) => {
            for (const n of node.childNodes) {
              if (n.nodeType === 3) { if (n.textContent) runs.push({ text: n.textContent, el: styleEl }); }
              else if (n.nodeType === 1 && n.tagName === 'BR') runs.push({ br: true });
              else if (n.nodeType === 1 && isInline(n)) w2(n, n);
            }
          };
          w2(c, c);
          const fmt = runs.map(rn => {
            if (rn.br) return { br: true };
            const s = getComputedStyle(rn.el);
            let text = rn.text.replace(/\s+/g, ' ');
            if (s.textTransform === 'uppercase') text = text.toUpperCase();
            const col = rgba(s.color);
            return { text, font: s.fontFamily.split(',')[0].replace(/["']/g, '').trim(), size: parseFloat(s.fontSize), bold: +s.fontWeight >= 600, italic: s.fontStyle === 'italic', color: col ? col.hex : '000000', ls: parseFloat(s.letterSpacing) || 0 };
          });
          if (fmt.length) { if (fmt[0].text) fmt[0].text = fmt[0].text.replace(/^ /, ''); const l = fmt[fmt.length - 1]; if (l.text) l.text = l.text.replace(/ $/, ''); }
          const flexCenter = cs.display === 'flex' && cs.justifyContent === 'center';
          const lh = cs.lineHeight === 'normal' ? parseFloat(cs.fontSize) * 1.15 : parseFloat(cs.lineHeight);
          const pl = parseFloat(cs.paddingLeft) || 0;
          push(c, { kind: 'text', x: b.x + pl, y: b.y, w: b.w - pl, h: b.h, align: flexCenter ? 'center' : (cs.textAlign === 'right' ? 'right' : cs.textAlign === 'center' ? 'center' : 'left'), valign: flexCenter || (cs.alignItems === 'center' && cs.display === 'flex') ? 'middle' : 'top', lh, runs: fmt, explicitW: c.style.width !== '' || flexCenter });
        }
        walk(c);
      }
    };
    walk(sec);
    items.sort((a, b) => a.z - b.z || a.o - b.o);
    res.push({ section: sec.dataset.section, notes: sec.querySelector('aside.notes')?.textContent.trim() || '', top: sr.top + window.scrollY, items });
  });
  return res;
});

// isolated raster screenshots: hide everything except the target
await page.addStyleTag({ content: `
  body.iso{background:transparent!important}
  body.iso section.slide{background:transparent!important}
  body.iso section.slide *{visibility:hidden!important}
  body.iso section.slide .iso-on, body.iso section.slide .iso-on *{visibility:visible!important}
  body.iso section.slide .iso-bg > *{visibility:hidden!important}` });
await page.evaluate(() => document.body.classList.add('iso'));
for (const [si, sd] of slides.entries()) {
  for (const it of sd.items.filter(i => i.kind === 'raster')) {
    await page.evaluate((id, bgOnly) => { const el = document.querySelector(`[data-rid="${id}"]`); el.classList.add('iso-on'); if (bgOnly) el.classList.add('iso-bg'); }, it.id, !!it.bgOnly);
    const clip = { x: Math.max(0, it.x), y: sd.top + Math.max(0, it.y), width: Math.min(1920 - Math.max(0, it.x), it.w + Math.min(0, it.x)), height: Math.min(1080 - Math.max(0, it.y), it.h + Math.min(0, it.y)) };
    it.x = Math.max(0, it.x); it.y = Math.max(0, it.y); it.w = clip.width; it.h = clip.height;
    it.file = `${it.id}.png`;
    await page.screenshot({ path: path.join(out, it.file), clip, omitBackground: true });
    await page.evaluate(id => { const el = document.querySelector(`[data-rid="${id}"]`); el.classList.remove('iso-on', 'iso-bg'); }, it.id);
  }
  console.log('slide', si + 1, 'rasters', sd.items.filter(i => i.kind === 'raster').length);
}
// slide backgrounds: the page gradient only
await page.evaluate(() => document.body.classList.remove('iso'));
await page.addStyleTag({ content: `body.bgonly section.slide *{visibility:hidden!important}` });
await page.evaluate(() => document.body.classList.add('bgonly'));
await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1.5 });
const secs = await page.$$('section.slide');
for (let i = 0; i < secs.length; i++) {
  slides[i].bg = `bg${String(i + 1).padStart(2, '0')}.jpg`;
  await secs[i].screenshot({ path: path.join(out, slides[i].bg), type: 'jpeg', quality: 95 });
}
await browser.close();

// photos: crop exactly what object-fit shows, at source resolution, rounded corners baked as alpha
let n = 0;
for (const sd of slides) for (const it of sd.items.filter(i => i.kind === 'img')) {
  const src = path.join(root, it.src);
  const { nw, nh, w, h } = it;
  let [px, py] = (it.pos || '50% 50%').split(' ').map(v => parseFloat(v) / 100);
  let cx = 0, cy = 0, cw = nw, ch = nh;
  if (it.fit === 'cover') {
    const s = Math.max(w / nw, h / nh); cw = Math.round(w / s); ch = Math.round(h / s);
    cx = Math.round((nw - cw) * px); cy = Math.round((nh - ch) * py);
  }
  let img = sharp(src).extract({ left: cx, top: cy, width: Math.min(cw, nw - cx), height: Math.min(ch, nh - cy) });
  const ow = Math.min(cw, nw - cx), oh = Math.min(ch, nh - cy);
  if (it.fit === 'contain') { // letterbox handled by placing the picture at its displayed size
    const s = Math.min(w / nw, h / nh); const dw = nw * s, dh = nh * s;
    it.x += (w - dw) / 2; it.y += (h - dh) / 2; it.w = dw; it.h = dh; it.r = 0;
  }
  // rounded corners are drawn by PowerPoint (rounded-rectangle picture geometry), so photos stay JPEG
  it.file = `img${n++}.jpg`;
  await img.jpeg({ quality: 92 }).toFile(path.join(out, it.file));
}
fs.writeFileSync(path.join(out, 'slides.json'), JSON.stringify(slides, null, 1));
const count = k => slides.reduce((a, s) => a + s.items.filter(i => i.kind === k).length, 0);
console.log('shapes', count('shape'), 'images', count('img'), 'rasters', count('raster'), 'text', count('text'));
