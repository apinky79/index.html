import { buildVCard, downloadText } from "./vcard.js";
import qrcode from "./qrcode.mjs";

const STORAGE_KEY = "digital-business-card-v1";

const defaults = {
  name: "Alex Morgan",
  title: "Founder & CEO",
  company: "Northbridge Studio",
  phone: "+1 415 555 0142",
  email: "alex@northbridgestudio.com",
  website: "https://northbridgestudio.com",
  linkedin: "https://linkedin.com/in/alexmorgan",
  location: "San Francisco, CA",
  tagline: "Design · Strategy · Product",
  brandColor: "#0f172a",
  accentColor: "#38bdf8",
  textColor: "#ffffff",
  passTypeIdentifier: "pass.com.yourcompany.businesscard",
  teamIdentifier: "YOUR_TEAM_ID",
  organizationName: "Northbridge Studio",
};

const fields = [
  "name",
  "title",
  "company",
  "phone",
  "email",
  "website",
  "linkedin",
  "location",
  "tagline",
  "brandColor",
  "accentColor",
  "textColor",
  "organizationName",
  "passTypeIdentifier",
  "teamIdentifier",
];

function readForm() {
  const data = {};
  for (const key of fields) {
    const el = document.getElementById(key);
    data[key] = el ? el.value.trim() : "";
  }
  return data;
}

function writeForm(data) {
  for (const key of fields) {
    const el = document.getElementById(key);
    if (el) el.value = data[key] ?? "";
  }
}

function slug(name) {
  return (name || "contact")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function qrPayload(data) {
  if (data.website) return data.website;
  if (data.email) return `mailto:${data.email}`;
  return buildVCard(data).slice(0, 400);
}

function renderQr(data) {
  const canvas = document.getElementById("qr-canvas");
  if (!canvas) return;
  const text = qrPayload(data);
  const qr = qrcode(0, "M");
  qr.addData(text);
  qr.make();
  const count = qr.getModuleCount();
  const size = canvas.width;
  const cell = Math.floor(size / count);
  const offset = Math.floor((size - cell * count) / 2);
  const ctx = canvas.getContext("2d");
  ctx.fillStyle = "#ffffff";
  ctx.fillRect(0, 0, size, size);
  ctx.fillStyle = "#000000";
  for (let row = 0; row < count; row++) {
    for (let col = 0; col < count; col++) {
      if (qr.isDark(row, col)) {
        ctx.fillRect(offset + col * cell, offset + row * cell, cell, cell);
      }
    }
  }
}

function renderPreview(data) {
  const root = document.documentElement;
  root.style.setProperty("--wallet-bg", data.brandColor || defaults.brandColor);
  root.style.setProperty("--wallet-fg", data.textColor || defaults.textColor);
  root.style.setProperty("--wallet-label", data.accentColor || defaults.accentColor);

  const set = (id, value) => {
    const el = document.getElementById(id);
    if (el) el.textContent = value || "—";
  };

  set("preview-name", data.name);
  set("preview-tagline", data.tagline);
  set("preview-title", data.title);
  set("preview-company", data.company);
  set("preview-phone", data.phone);
  set("preview-email", data.email);
  renderQr(data);
}

function save(data) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

function load() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { ...defaults };
    return { ...defaults, ...JSON.parse(raw) };
  } catch {
    return { ...defaults };
  }
}

function exportConfig(data) {
  downloadText("card.config.json", "application/json", JSON.stringify(data, null, 2) + "\n");
}

function bind() {
  const form = document.getElementById("card-form");
  const onChange = () => {
    const data = readForm();
    save(data);
    renderPreview(data);
  };

  form?.addEventListener("input", onChange);

  document.getElementById("btn-vcard")?.addEventListener("click", () => {
    const data = readForm();
    const vcf = buildVCard(data);
    downloadText(`${slug(data.name)}.vcf`, "text/vcard;charset=utf-8", vcf);
  });

  document.getElementById("btn-export-config")?.addEventListener("click", () => {
    exportConfig(readForm());
  });

  document.getElementById("btn-reset")?.addEventListener("click", () => {
    writeForm(defaults);
    save(defaults);
    renderPreview(defaults);
  });

  const initial = load();
  writeForm(initial);
  renderPreview(initial);
}

bind();
