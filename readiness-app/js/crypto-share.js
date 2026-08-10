/**
 * Encrypt/decrypt health data for secure sharing.
 * Passphrase is never stored — share file + passphrase separately.
 */

async function deriveKey(passphrase, salt) {
  const enc = new TextEncoder();
  const keyMaterial = await crypto.subtle.importKey(
    'raw',
    enc.encode(passphrase),
    'PBKDF2',
    false,
    ['deriveKey']
  );
  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt, iterations: 100000, hash: 'SHA-256' },
    keyMaterial,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt', 'decrypt']
  );
}

function bufToBase64(buf) {
  return btoa(String.fromCharCode(...new Uint8Array(buf)));
}

function base64ToBuf(str) {
  return Uint8Array.from(atob(str), (c) => c.charCodeAt(0));
}

export async function encryptExport(data, passphrase) {
  if (!passphrase || passphrase.length < 8) {
    throw new Error('Passphrase must be at least 8 characters.');
  }
  const salt = crypto.getRandomValues(new Uint8Array(16));
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const key = await deriveKey(passphrase, salt);
  const enc = new TextEncoder();
  const ciphertext = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv },
    key,
    enc.encode(JSON.stringify(data))
  );
  return {
    version: 1,
    algorithm: 'AES-GCM-PBKDF2',
    salt: bufToBase64(salt),
    iv: bufToBase64(iv),
    payload: bufToBase64(ciphertext),
    exportedAt: new Date().toISOString(),
  };
}

export async function decryptExport(encrypted, passphrase) {
  const salt = base64ToBuf(encrypted.salt);
  const iv = base64ToBuf(encrypted.iv);
  const ciphertext = base64ToBuf(encrypted.payload);
  const key = await deriveKey(passphrase, salt);
  const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, ciphertext);
  return JSON.parse(new TextDecoder().decode(plain));
}

export function downloadEncrypted(data, filename) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = filename;
  a.click();
}
