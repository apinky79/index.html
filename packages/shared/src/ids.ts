/** Cryptographic helpers for content fingerprints (duplicate detection). */
import { createHash } from 'node:crypto';

export function sha256Hex(input: string | Buffer): string {
  return createHash('sha256').update(input).digest('hex');
}

export function createId(prefix: string): string {
  const rand = cryptoRandom();
  return `${prefix}_${rand}`;
}

function cryptoRandom(): string {
  return createHash('sha256')
    .update(`${Date.now()}-${Math.random()}-${process.hrtime.bigint()}`)
    .digest('hex')
    .slice(0, 26);
}
