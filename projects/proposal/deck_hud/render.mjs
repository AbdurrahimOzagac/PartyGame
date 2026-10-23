// Split each HTML slide into (a) a text-free background render and (b) native text boxes.
// node render.mjs slides.html outdir
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import puppeteer from 'puppeteer-core';

const [file = 'slides.html', outDir = 'out'] = process.argv.slice(2);
const root = path.dirname(new URL(import.meta.url).pathname);
fs.mkdirSync(path.join(root, outDir), { recursive: true });
const types = { '.html': 'text/html', '.js': 'text/javascript', '.png': 'image/png', '.jpg': 'image/jpeg' };
const server = http.createServer((req, res) => {
  const f = path.join(root, decodeURIComponent(req.url.split('?')[0]));
  if (!f.startsWith(root) || !fs.existsSync(f)) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'Content-Type': types[path.extname(f)] || 'application/octet-stream' });
  fs.createReadStream(f).pipe(res);
}).listen(0);

const browser = await puppeteer.launch({ executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true });
const page = await browser.newPage();
page.on('pageerror', e => console.error('[pageerror]', e.message));
await page.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1.5 });
await page.goto(`http://localhost:${server.address().port}/${file}`, { waitUntil: 'networkidle0' });
await page.waitForFunction('window.READY === true');
await page.evaluate(() => document.fonts.ready);

const slides = await page.evaluate(() => {
  const hex = c => { const m = c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const [r, g, b, a = 1] = m[1].split(',').map(Number); if (a < 0.05) return null; return [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('').toUpperCase(); };
  const isInline = el => getComputedStyle(el).display === 'inline';
  const out = [];
  document.querySelectorAll('section.slide').forEach((sec, si) => {
    const sr = sec.getBoundingClientRect();
    const boxes = [];
    const taken = new Set();
    const all = sec.querySelectorAll('*');
    for (const el of all) {
      if (taken.has(el) || el.closest('aside.notes,svg') || isInline(el)) continue;
      // box = non-inline element with text directly or through inline children
      const own = [...el.childNodes].some(n => (n.nodeType === 3 && n.textContent.trim()) || (n.nodeType === 1 && isInline(n) && n.textContent.trim() && n.tagName !== 'svg'));
      if (!own) continue;
      const runs = [];
      const walk = (node, styleEl) => {
        for (const n of node.childNodes) {
          if (n.nodeType === 3) { if (n.textContent) runs.push({ text: n.textContent, el: styleEl }); }
          else if (n.nodeType === 1 && n.tagName === 'BR') runs.push({ br: true });
          else if (n.nodeType === 1 && isInline(n)) { taken.add(n); walk(n, n); }
        }
      };
      walk(el, el);
      const cs = getComputedStyle(el);
      const r = el.getBoundingClientRect();
      const fmt = runs.map(rn => {
        if (rn.br) return { br: true };
        const s = getComputedStyle(rn.el);
        let text = rn.text.replace(/\s+/g, ' ');
        if (s.textTransform === 'uppercase') text = text.toUpperCase();
        return { text, font: s.fontFamily.split(',')[0].replace(/["']/g, '').trim(), size: parseFloat(s.fontSize), bold: +s.fontWeight >= 600, italic: s.fontStyle === 'italic', color: hex(s.color) || '000000', ls: parseFloat(s.letterSpacing) || 0 };
      });
      if (fmt.length) { fmt[0].text = fmt[0].text?.replace(/^ /, ''); const l = fmt[fmt.length - 1]; if (l.text) l.text = l.text.replace(/ $/, ''); }
      const flexCenter = cs.display === 'flex' && cs.justifyContent === 'center';
      const lh = cs.lineHeight === 'normal' ? parseFloat(cs.fontSize) * 1.15 : parseFloat(cs.lineHeight);
      boxes.push({ x: r.left - sr.left, y: r.top - sr.top, w: r.width, h: r.height, align: flexCenter ? 'center' : (cs.textAlign === 'right' ? 'right' : cs.textAlign === 'center' ? 'center' : 'left'), valign: flexCenter || cs.alignItems === 'center' && cs.display === 'flex' ? 'middle' : 'top', lh, runs: fmt, explicitW: el.style.width !== '' || flexCenter });
      el.dataset.textbox = '1';
    }
    out.push({ section: sec.dataset.section, notes: sec.querySelector('aside.notes')?.textContent.trim() || '', boxes });
  });
  // hide every collected text, keep everything else
  const st = document.createElement('style');
  st.textContent = '[data-textbox], [data-textbox] * { color: transparent !important; -webkit-text-fill-color: transparent !important; }';
  document.head.appendChild(st);
  return out;
});

const secs = await page.$$('section.slide');
for (let i = 0; i < secs.length; i++) {
  const f = `bg${String(i + 1).padStart(2, '0')}.jpg`;
  await secs[i].screenshot({ path: path.join(root, outDir, f), type: 'jpeg', quality: 90 });
  slides[i].bg = f;
}
fs.writeFileSync(path.join(root, outDir, 'slides.json'), JSON.stringify(slides, null, 1));
console.log('slides', slides.length, 'text boxes', slides.reduce((a, s) => a + s.boxes.length, 0));
await browser.close();
server.close();
