import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { protectDirectory, SessionStore, gatewayToken } from '../src/storage.mjs';

test('Windows ACL protection, token durability and atomic session state survive reconstruction', context => {
  const directory = mkdtempSync(join(tmpdir(), 'lumar-wa-storage-test-'));
  context.after(() => rmSync(directory, { recursive: true }));
  protectDirectory(directory);
  const store = new SessionStore(directory);
  assert.deepEqual(store.read(), { linked: false, connect: false, loggedOut: false });
  store.write({ linked: true, connect: true, loggedOut: false });
  assert.deepEqual(new SessionStore(directory).read(), { linked: true, connect: true, loggedOut: false });
  const token = gatewayToken(directory, true);
  assert.equal(token.length, 64);
  assert.equal(gatewayToken(directory) === token, true);
  assert.equal(readFileSync(join(directory, 'state.json'), 'utf8').includes(token), false);
  store.write({ linked: true, connect: false, loggedOut: false });
  assert.equal(new SessionStore(directory).read().connect, false);
  protectDirectory(directory);
  assert.equal(gatewayToken(directory) === token, true);
  assert.equal(new SessionStore(directory).read().connect, false);
});