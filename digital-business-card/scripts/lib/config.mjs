import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const ROOT = path.join(__dirname, "../..");

export function loadCardConfig() {
  const configPath = path.join(ROOT, "card.config.json");
  const examplePath = path.join(ROOT, "card.config.example.json");
  const source = fs.existsSync(configPath) ? configPath : examplePath;
  const raw = fs.readFileSync(source, "utf8");
  return { config: JSON.parse(raw), configPath: source };
}

export function hexToRgb(hex) {
  const normalized = hex.replace("#", "").trim();
  const full =
    normalized.length === 3
      ? normalized
          .split("")
          .map((c) => c + c)
          .join("")
      : normalized;
  const n = parseInt(full, 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  return `rgb(${r}, ${g}, ${b})`;
}

export function initials(name) {
  return (name || "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

export function buildVCard(config) {
  const lines = [
    "BEGIN:VCARD",
    "VERSION:3.0",
    `FN:${escapeVCard(config.name)}`,
    `N:${escapeVCard(familyGiven(config.name))}`,
  ];
  if (config.title || config.company) {
    lines.push(`TITLE:${escapeVCard(config.title || "")}`);
    lines.push(`ORG:${escapeVCard(config.company || "")}`);
  }
  if (config.phone) {
    lines.push(`TEL;TYPE=CELL:${escapeVCard(config.phone)}`);
  }
  if (config.email) {
    lines.push(`EMAIL;TYPE=INTERNET:${escapeVCard(config.email)}`);
  }
  if (config.website) {
    lines.push(`URL:${escapeVCard(config.website)}`);
  }
  if (config.linkedin) {
    lines.push(`X-SOCIALPROFILE;TYPE=linkedin:${escapeVCard(config.linkedin)}`);
  }
  if (config.location) {
    lines.push(`ADR;TYPE=WORK:;;${escapeVCard(config.location)};;;;`);
  }
  if (config.tagline) {
    lines.push(`NOTE:${escapeVCard(config.tagline)}`);
  }
  lines.push("END:VCARD");
  return lines.join("\r\n");
}

function familyGiven(name) {
  const parts = (name || "").trim().split(/\s+/);
  if (parts.length <= 1) return `;${parts[0] || ""};;;`;
  const given = parts.slice(0, -1).join(" ");
  const family = parts[parts.length - 1];
  return `${family};${given};;;`;
}

function escapeVCard(value) {
  return String(value ?? "")
    .replace(/\\/g, "\\\\")
    .replace(/;/g, "\\;")
    .replace(/,/g, "\\,")
    .replace(/\n/g, "\\n");
}
