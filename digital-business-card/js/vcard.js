export function escapeVCard(value) {
  return String(value ?? "")
    .replace(/\\/g, "\\\\")
    .replace(/;/g, "\\;")
    .replace(/,/g, "\\,")
    .replace(/\n/g, "\\n");
}

function familyGiven(name) {
  const parts = (name || "").trim().split(/\s+/);
  if (parts.length <= 1) return `;${parts[0] || ""};;;`;
  const given = parts.slice(0, -1).join(" ");
  const family = parts[parts.length - 1];
  return `${family};${given};;;`;
}

export function buildVCard(data) {
  const lines = [
    "BEGIN:VCARD",
    "VERSION:3.0",
    `FN:${escapeVCard(data.name)}`,
    `N:${escapeVCard(familyGiven(data.name))}`,
  ];
  if (data.title) lines.push(`TITLE:${escapeVCard(data.title)}`);
  if (data.company) lines.push(`ORG:${escapeVCard(data.company)}`);
  if (data.phone) lines.push(`TEL;TYPE=CELL:${escapeVCard(data.phone)}`);
  if (data.email) lines.push(`EMAIL;TYPE=INTERNET:${escapeVCard(data.email)}`);
  if (data.website) lines.push(`URL:${escapeVCard(data.website)}`);
  if (data.linkedin) lines.push(`X-SOCIALPROFILE;TYPE=linkedin:${escapeVCard(data.linkedin)}`);
  if (data.location) lines.push(`ADR;TYPE=WORK:;;${escapeVCard(data.location)};;;;`);
  if (data.tagline) lines.push(`NOTE:${escapeVCard(data.tagline)}`);
  lines.push("END:VCARD");
  return lines.join("\r\n");
}

export function downloadText(filename, mime, text) {
  const blob = new Blob([text], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
