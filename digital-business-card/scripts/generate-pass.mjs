import fs from "node:fs";
import path from "node:path";
import { PKPass } from "passkit-generator";
import { ROOT, loadCardConfig, buildVCard } from "./lib/config.mjs";

const { config } = loadCardConfig();
const modelDir = path.join(ROOT, "BusinessCard.pass");
const outputDir = path.join(ROOT, "output");

function readCert(filePath) {
  if (!fs.existsSync(filePath)) {
    return null;
  }
  return fs.readFileSync(filePath);
}

function loadCertificates() {
  const certDir = process.env.APPLE_CERT_DIR || path.join(ROOT, "certs");
  const wwdr = readCert(process.env.APPLE_WWDR_PATH || path.join(certDir, "wwdr.pem"));
  const signerCert = readCert(
    process.env.APPLE_SIGNER_CERT_PATH || path.join(certDir, "signerCert.pem")
  );
  const signerKey = readCert(
    process.env.APPLE_SIGNER_KEY_PATH || path.join(certDir, "signerKey.pem")
  );
  const signerKeyPassphrase = process.env.APPLE_SIGNER_KEY_PASSPHRASE || undefined;

  if (!wwdr || !signerCert || !signerKey) {
    return null;
  }
  return { wwdr, signerCert, signerKey, signerKeyPassphrase };
}

function field(key, label, value) {
  if (!value) return null;
  return { key, label, value: String(value) };
}

function buildGenericFields() {
  const primary = [field("name", "NAME", config.name)].filter(Boolean);
  const secondary = [
    field("title", "TITLE", config.title),
    field("company", "COMPANY", config.company),
  ].filter(Boolean);
  const auxiliary = [
    field("phone", "PHONE", config.phone),
    field("email", "EMAIL", config.email),
  ].filter(Boolean);

  const back = [
    field("website", "Website", config.website),
    field("linkedin", "LinkedIn", config.linkedin),
    field("location", "Location", config.location),
    field("tagline", "About", config.tagline),
    {
      key: "vcard",
      label: "Contact file",
      value: "Scan the QR code or share the vCard from the web app.",
    },
  ].filter(Boolean);

  return { primaryFields: primary, secondaryFields: secondary, auxiliaryFields: auxiliary, backFields: back };
}

const certificates = loadCertificates();
if (!certificates) {
  console.error(`
Missing Apple Wallet signing certificates.

To install a pass on iPhone, Apple requires a signed .pkpass file from your Apple Developer account.

1. Copy card.config.example.json to card.config.json and fill in your details.
2. Create a Pass Type ID and certificate — see digital-business-card/WALLET-SIGNING.md
3. Place these files in digital-business-card/certs/:
   - wwdr.pem
   - signerCert.pem
   - signerKey.pem

Then run: npm run build
`);
  process.exit(1);
}

if (!fs.existsSync(path.join(modelDir, "icon.png"))) {
  console.error("Run npm run generate:assets first.");
  process.exit(1);
}

const serial = `card-${Date.now()}`;
const qrTarget =
  config.website ||
  (config.email ? `mailto:${config.email}` : buildVCard(config).slice(0, 280));

const generic = buildGenericFields();

const pass = await PKPass.from(
  {
    model: modelDir,
    certificates,
  },
  {
    serialNumber: serial,
    generic,
  }
);

pass.setBarcodes({
  format: "PKBarcodeFormatQR",
  message: qrTarget,
  messageEncoding: "iso-8859-1",
  altText: config.website ? "Open website" : "Contact",
});

fs.mkdirSync(outputDir, { recursive: true });
const slug = (config.name || "business-card")
  .toLowerCase()
  .replace(/[^a-z0-9]+/g, "-")
  .replace(/^-|-$/g, "");
const outFile = path.join(outputDir, `${slug || "business-card"}.pkpass`);
fs.writeFileSync(outFile, pass.getAsBuffer());

console.log(`Signed Apple Wallet pass: ${outFile}`);
console.log("Transfer to iPhone (AirDrop, Mail, or HTTPS with application/vnd.apple.pkpass) and tap Add.");
