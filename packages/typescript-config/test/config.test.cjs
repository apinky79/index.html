const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

test('typescript config fragments exist and parse', () => {
  const root = path.join(__dirname, '..');
  for (const file of ['base.json', 'react.json']) {
    const full = path.join(root, file);
    assert.ok(fs.existsSync(full), `${file} missing`);
    const json = JSON.parse(fs.readFileSync(full, 'utf8'));
    assert.equal(typeof json, 'object');
  }
});
