/**
 * Environment helpers.
 * Secrets and runtime overrides enter through process.env — never hard-code secrets.
 */

export type EnvSource = Record<string, string | undefined>;

/**
 * Read an optional environment variable.
 */
export function readEnv(name: string, source: EnvSource = process.env): string | undefined {
  const value = source[name];
  if (value === undefined || value.trim() === '') {
    return undefined;
  }
  return value;
}

/**
 * Read a required environment variable or throw.
 */
export function requireEnv(name: string, source: EnvSource = process.env): string {
  const value = readEnv(name, source);
  if (value === undefined) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}
