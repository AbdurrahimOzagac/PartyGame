// node preview.mjs [slideNumbers...] → qa/sNN.png full renders (with text) for review
import fs from 'node:fs'; import path from 'node:path'; import puppeteer from 'puppeteer-core';
const root = path.dirname(new URL(import.meta.url).pathname);
fs.mkdirSync(path.join(root, 'qa'), { recursive: true });
const only = process.argv.slice(2).map(Number);
const b = await puppeteer.launch({ executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true, args: ['--allow-file-access-from-files'] });
const p = await b.newPage(); p.on('pageerror', e => console.error('[pageerror]', e.message));
await p.setViewport({ width: 1920, height: 1080, deviceScaleFactor: 0.5 });
await p.goto('file://' + path.join(root, 'slides.html'), { waitUntil: 'networkidle0' });
await p.waitForFunction('window.READY === true');
const secs = await p.$$('section.slide');
for (let i = 0; i < secs.length; i++) if (!only.length || only.includes(i + 1)) await secs[i].screenshot({ path: path.join(root, 'qa', `s${String(i + 1).padStart(2, '0')}.png`) });
await b.close();
