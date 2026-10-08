import fs from "node:fs";
import path from "node:path";
import sharp from "sharp";
import { ROOT, loadCardConfig, hexToRgb, initials } from "./lib/config.mjs";

const { config } = loadCardConfig();
const modelDir = path.join(ROOT, "BusinessCard.pass");

function svgIcon(size, letter, bg, fg) {
  const fontSize = Math.round(size * 0.42);
  return `<svg width="${size}" height="${size}" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" rx="${Math.round(size * 0.22)}" fill="${bg}"/>
  <text x="50%" y="54%" dominant-baseline="middle" text-anchor="middle"
    font-family="system-ui, -apple-system, sans-serif" font-weight="600"
    font-size="${fontSize}" fill="${fg}">${letter}</text>
</svg>`;
}

function svgLogo(name, company, width, height, fg, accent) {
  const titleSize = 22;
  const subSize = 14;
  const safeName = escapeXml(name);
  const safeCo = escapeXml(company || "");
  return `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
  <text x="0" y="28" font-family="system-ui, -apple-system, sans-serif" font-weight="600"
    font-size="${titleSize}" fill="${fg}">${safeName}</text>
  ${
    safeCo
      ? `<text x="0" y="46" font-family="system-ui, -apple-system, sans-serif" font-weight="500"
    font-size="${subSize}" fill="${accent}">${safeCo}</text>`
      : ""
  }
</svg>`;
}

function svgStrip(width, height, name, tagline, bg, fg, accent) {
  return `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="${bg}"/>
      <stop offset="100%" stop-color="${accent}" stop-opacity="0.35"/>
    </linearGradient>
  </defs>
  <rect width="100%" height="100%" fill="url(#g)"/>
  <text x="24" y="52" font-family="system-ui, -apple-system, sans-serif" font-weight="600"
    font-size="28" fill="${fg}">${escapeXml(name)}</text>
  ${
    tagline
      ? `<text x="24" y="82" font-family="system-ui, -apple-system, sans-serif" font-weight="500"
    font-size="16" fill="${fg}" opacity="0.85">${escapeXml(tagline)}</text>`
      : ""
  }
</svg>`;
}

function escapeXml(s) {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

async function writePng(filename, svg, width, height) {
  const out = path.join(modelDir, filename);
  await sharp(Buffer.from(svg)).resize(width, height).png().toFile(out);
}

const letter = initials(config.name);
const bg = config.brandColor || "#0f172a";
const fg = config.textColor || "#ffffff";
const accent = config.accentColor || "#38bdf8";

await writePng("icon.png", svgIcon(29, letter, bg, fg), 29, 29);
await writePng("icon@2x.png", svgIcon(58, letter, bg, fg), 58, 58);
await writePng("icon@3x.png", svgIcon(87, letter, bg, fg), 87, 87);

await writePng("logo.png", svgLogo(config.name, config.company, 160, 50, fg, accent), 160, 50);
await writePng(
  "logo@2x.png",
  svgLogo(config.name, config.company, 320, 100, fg, accent),
  320,
  100
);
await writePng(
  "logo@3x.png",
  svgLogo(config.name, config.company, 480, 150, fg, accent),
  480,
  150
);

await writePng(
  "strip.png",
  svgStrip(375, 123, config.name, config.tagline, bg, fg, accent),
  375,
  123
);
await writePng(
  "strip@2x.png",
  svgStrip(750, 246, config.name, config.tagline, bg, fg, accent),
  750,
  246
);
await writePng(
  "strip@3x.png",
  svgStrip(1125, 369, config.name, config.tagline, bg, fg, accent),
  1125,
  369
);

const passJsonPath = path.join(modelDir, "pass.json");
const passBase = JSON.parse(fs.readFileSync(passJsonPath, "utf8"));
passBase.passTypeIdentifier = config.passTypeIdentifier || passBase.passTypeIdentifier;
passBase.teamIdentifier = config.teamIdentifier || passBase.teamIdentifier;
passBase.organizationName = config.organizationName || config.company || passBase.organizationName;
passBase.foregroundColor = hexToRgb(fg);
passBase.backgroundColor = hexToRgb(bg);
passBase.labelColor = hexToRgb(accent);
passBase.logoText = " ";
passBase.description = `${config.name} — business card`;
fs.writeFileSync(passJsonPath, JSON.stringify(passBase, null, 2));

console.log(`Updated Wallet pass assets in ${modelDir}`);
