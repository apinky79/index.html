import assert from 'node:assert/strict';
import test from 'node:test';

import config from '../index.js';

test('eslint config exports a non-empty flat config array', () => {
  assert.ok(Array.isArray(config));
  assert.ok(config.length > 0);
});
