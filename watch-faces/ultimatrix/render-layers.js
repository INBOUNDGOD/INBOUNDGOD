// Renders the Ultimatrix watch face layers (450x450 PNG + SVG source) for Watch Face Studio.
// Run from this folder: NODE_PATH=$(npm root -g) node render-layers.js
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const C = 225, GREEN = '#62e83c';
const FONT_SEMI = fs.readFileSync(path.join(__dirname, 'fonts/Oxanium-SemiBold.ttf')).toString('base64');
const FONT_BOLD = fs.readFileSync(path.join(__dirname, 'fonts/Oxanium-Bold.ttf')).toString('base64');
const FONT_CSS = `@font-face{font-family:Ox;font-weight:600;src:url(data:font/ttf;base64,${FONT_SEMI})}@font-face{font-family:Ox;font-weight:700;src:url(data:font/ttf;base64,${FONT_BOLD})}`;

function pt(r, deg) { const a = (deg - 90) * Math.PI / 180; return [C + r * Math.cos(a), C + r * Math.sin(a)]; }
const f = n => n.toFixed(2);
function sector(r, d1, d2) { const [x1, y1] = pt(r, d1), [x2, y2] = pt(r, d2); return `M${C} ${C}L${f(x1)} ${f(y1)}A${r} ${r} 0 0 1 ${f(x2)} ${f(y2)}Z`; }
const hourglass = (r, half) => sector(r, -half, half) + sector(r, 180 - half, 180 + half);
function segments(fill) {
  let s = '';
  for (let k = 0; k < 60; k++) {
    const big = k % 15 === 0;
    s += `<rect x="${big ? 220.5 : 222}" y="${big ? 9 : 12}" width="${big ? 9 : 6}" height="${big ? 28 : 21}" rx="1.5" fill="${fill}" transform="rotate(${k * 6} 225 225)"/>`;
  }
  return s;
}
const svg = (inner, defs = '') => `<svg xmlns="http://www.w3.org/2000/svg" width="450" height="450" viewBox="0 0 450 450"><defs><style>${FONT_CSS}</style>${defs}</defs>${inner}</svg>`;
const PHONE_ICON = '<rect x="4.5" y="1.5" width="9" height="15" rx="2"/><path d="M7.5 14h3"/><path d="M9.8 4.8 7.6 8.6h2.8l-2.2 3.8"/>';
// Box kept clear of hourglass lines in Always-On so they don't cross the digits
const TIME_BOX = { x: 92, y: 166, w: 266, h: 96 };

const LAYERS = {
  // ---- Active (wrist up) ----
  '01_background': svg(
    `<circle cx="225" cy="225" r="225" fill="#000"/>
     <circle cx="225" cy="225" r="201" fill="none" stroke="url(#ring)" stroke-width="48"/>
     ${segments('#17241a')}
     <circle cx="225" cy="225" r="175" fill="#030504" stroke="${GREEN}" stroke-width="2.5"/>
     <circle cx="225" cy="225" r="168" fill="url(#bg)"/>
     <path d="${hourglass(150, 44)}" fill="${GREEN}" fill-opacity=".07" stroke="${GREEN}" stroke-opacity=".45" stroke-width="3"/>`,
    `<radialGradient id="bg" cx=".5" cy=".5" r=".5"><stop offset="0" stop-color="#123a19"/><stop offset=".6" stop-color="#06140a"/><stop offset="1" stop-color="#010302"/></radialGradient>
     <linearGradient id="ring" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2a3033"/><stop offset="1" stop-color="#0b0d0e"/></linearGradient>`),
  // Sits ABOVE the seconds progress bar: solid bezel with see-through slots, so the green bar only shows inside the slots
  '03_bezel_overlay': svg(
    `<circle cx="225" cy="225" r="201" fill="none" stroke="url(#ring)" stroke-width="48" mask="url(#holes)"/>`,
    `<linearGradient id="ring" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2a3033"/><stop offset="1" stop-color="#0b0d0e"/></linearGradient>
     <mask id="holes" maskUnits="userSpaceOnUse" x="0" y="0" width="450" height="450"><rect width="450" height="450" fill="#fff"/>${segments('#000')}</mask>`),
  '05_phone_icon_label': svg(
    `<g fill="none" stroke="${GREEN}" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" transform="translate(216 262)">${PHONE_ICON}</g>
     <text x="225" y="325" font-family="Ox" font-weight="600" font-size="10" letter-spacing="2" fill="${GREEN}" text-anchor="middle" dominant-baseline="central">PHONE</text>`),
  // ---- Always-On ----
  'aod_01_static': svg(
    `<g clip-path="url(#notime)"><path d="${hourglass(150, 44)}" fill="none" stroke="${GREEN}" stroke-width="3.5"/></g>
     <circle cx="225" cy="225" r="175" fill="none" stroke="#2f8f22" stroke-width="2"/>`,
    `<clipPath id="notime"><path clip-rule="evenodd" d="M0 0H450V450H0Z M${TIME_BOX.x} ${TIME_BOX.y}h${TIME_BOX.w}v${TIME_BOX.h}h-${TIME_BOX.w}Z"/></clipPath>`),
  // Seconds hand image: points to 12, rotates around the centre (225,225)
  'aod_02_seconds_tick': svg(`<rect x="221" y="10" width="8" height="26" rx="1.5" fill="#9dff6e"/>`),
};

// Previews: layers plus the text Watch Face Studio will draw, at sample values
const T = (x, y, size, weight, fill, txt, extra = '') => `<text x="${x}" y="${y}" font-family="Ox" font-weight="${weight}" font-size="${size}" fill="${fill}" text-anchor="middle" dominant-baseline="central" ${extra}>${txt}</text>`;
function litSeconds(sec) { // what the progress bar shows through the slots
  return `<path d="${arc(202, -3, sec * 6 + 3)}" fill="none" stroke="${GREEN}" stroke-width="30"/>`;
}
function arc(r, d1, d2) { const [x1, y1] = pt(r, d1), [x2, y2] = pt(r, d2); return `M${f(x1)} ${f(y1)}A${r} ${r} 0 ${d2 - d1 > 180 ? 1 : 0} 1 ${f(x2)} ${f(y2)}`; }
const img = name => `<image href="layers/${name}.png" width="450" height="450"/>`;

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 450, height: 450 }, deviceScaleFactor: 1 });
  async function render(markup, out) {
    await page.setContent(`<html><body style="margin:0;background:transparent">${markup}</body></html>`, { waitUntil: 'load' });
    await page.evaluate(() => document.fonts.ready);
    await page.locator('svg').first().screenshot({ path: out, omitBackground: true });
  }
  for (const [name, markup] of Object.entries(LAYERS)) {
    fs.writeFileSync(path.join(__dirname, 'svg', name + '.svg'), markup);
    await render(markup, path.join(__dirname, 'layers', name + '.png'));
    console.log('layer', name);
  }
  // previews: inline the PNG layers as data URIs
  const data = n => 'data:image/png;base64,' + fs.readFileSync(path.join(__dirname, 'layers', n + '.png')).toString('base64');
  const im = n => `<image href="${data(n)}" width="450" height="450"/>`;
  const active = svg(
    im('01_background') + litSeconds(41) + im('03_bezel_overlay') +
    T(225, 142, 17, 600, GREEN, 'FRI · OCT 02', 'letter-spacing="3"') +
    T(225, 214, 88, 700, '#effff0', '10:42', 'letter-spacing="1"') +
    im('05_phone_icon_label') + T(225, 303, 26, 600, '#effff0', '64%'));
  const aod = svg('<circle cx="225" cy="225" r="225" fill="#000"/>' + im('aod_01_static') +
    `<g transform="rotate(246 225 225)">${im('aod_02_seconds_tick')}</g>` +
    T(225, 214, 88, 700, '#3fbf28', '10:42', 'letter-spacing="1"'));
  await render(active, path.join(__dirname, 'preview', 'preview_active.png'));
  await render(aod, path.join(__dirname, 'preview', 'preview_aod.png'));
  console.log('previews done');
  await browser.close();
})();
