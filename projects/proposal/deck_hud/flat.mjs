// node flat.mjs → out_flat/sNN.jpg (full slide renders) + slides.json (sections, notes)
import fs from 'node:fs'; import path from 'node:path'; import puppeteer from 'puppeteer-core';
const root = path.dirname(new URL(import.meta.url).pathname), out = path.join(root, 'out_flat');
fs.rmSync(out, { recursive: true, force: true }); fs.mkdirSync(out);
const b = await puppeteer.launch({ executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true, args: ['--allow-file-access-from-files'] });
const p = await b.newPage(); p.on('pageerror', e => console.error('[pageerror]', e.message));
await p.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 1.5 });
await p.goto('file://' + path.join(root, 'slides.html'), { waitUntil: 'networkidle0' });
await p.waitForFunction('window.READY === true');
await p.evaluate(() => document.fonts.ready);
const meta = await p.evaluate(() => [...document.querySelectorAll('section.slide')].map(s => ({ section: s.dataset.section, notes: s.querySelector('aside.notes')?.textContent.trim() || '' })));
const secs = await p.$$('section.slide');
for (let i = 0; i < secs.length; i++) { meta[i].img = `s${String(i + 1).padStart(2, '0')}.jpg`; await secs[i].screenshot({ path: path.join(out, meta[i].img), type: 'jpeg', quality: 92 }); }
fs.writeFileSync(path.join(out, 'slides.json'), JSON.stringify(meta, null, 1));
console.log('rendered', secs.length); await b.close();
