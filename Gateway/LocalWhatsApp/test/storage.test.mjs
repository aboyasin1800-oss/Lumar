import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, readFileSync, statSync, mkdirSync, chmodSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { acquireInstanceLock, dataDirectory, protectDirectory, validateLinuxStorageDirectory, SessionStore, gatewayToken } from '../src/storage.mjs';

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

test('Linux storage directory and private files use POSIX permissions', { skip: process.platform !== 'linux' }, context => {
  const directory = mkdtempSync(join(tmpdir(), 'lumar-wa-linux-storage-test-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));
  process.env.LUMAR_LOCAL_WHATSAPP_DATA_DIRECTORY = directory;
  context.after(() => delete process.env.LUMAR_LOCAL_WHATSAPP_DATA_DIRECTORY);

  assert.equal(dataDirectory(), directory);
  protectDirectory(directory);
  assert.equal(statSync(directory).mode & 0o777, 0o700);

  const store = new SessionStore(directory);
  store.write({ linked: true, connect: true, loggedOut: false });
  assert.equal(statSync(join(directory, 'state.json')).mode & 0o777, 0o600);

  const token = gatewayToken(directory, true);
  assert.equal(token.length, 64);
  assert.equal(statSync(join(directory, 'gateway-token')).mode & 0o777, 0o600);

  const firstLock = acquireInstanceLock(directory);
  assert.throws(() => acquireInstanceLock(directory), /INSTANCE_LOCKED/);
  firstLock.close();
  const secondLock = acquireInstanceLock(directory);
  secondLock.close();

  const firstToken = gatewayToken(directory);
  gatewayToken(directory);
  assert.equal(gatewayToken(directory), firstToken);
});

test('Linux storage rejects empty, relative and unsafe paths', { skip: process.platform !== 'linux' }, () => {
  delete process.env.LUMAR_LOCAL_WHATSAPP_DATA_DIRECTORY;
  assert.throws(() => dataDirectory(), /LINUX_STORAGE_DIRECTORY_REQUIRED/);

  process.env.LUMAR_LOCAL_WHATSAPP_DATA_DIRECTORY = 'relative/data';
  assert.throws(() => dataDirectory(), /LINUX_STORAGE_DIRECTORY_REQUIRED/);
  delete process.env.LUMAR_LOCAL_WHATSAPP_DATA_DIRECTORY;
});

test('Linux storage rejects unsafe permissions, ownership, and symbolic links', { skip: process.platform !== 'linux' }, context => {
  const directory = mkdtempSync(join(tmpdir(), 'lumar-wa-linux-security-test-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));

  chmodSync(directory, 0o777);
  assert.throws(() => validateLinuxStorageDirectory(directory), /LINUX_STORAGE_PERMISSIONS_UNSAFE/);

  chmodSync(directory, 0o700);
  if (typeof process.getuid === 'function') {
    assert.throws(() => validateLinuxStorageDirectory(directory, process.getuid() + 1000), /LINUX_STORAGE_OWNERSHIP_INVALID/);
  }

  const link = join(directory, 'linked-state');
  symlinkSync(join(directory, 'target'), link);
  assert.throws(() => protectDirectory(link), /UNSAFE_STORAGE_PATH/);

  const authDirectory = join(directory, 'auth');
  mkdirSync(authDirectory);
  chmodSync(authDirectory, 0o777);
  assert.throws(() => protectDirectory(directory), /LINUX_STORAGE_PERMISSIONS_UNSAFE/);

  chmodSync(authDirectory, 0o700);
  const token = join(directory, 'gateway-token');
  writeFileSync(token, 'a'.repeat(64), { mode: 0o644 });
  assert.throws(() => gatewayToken(directory), /UNSAFE_TOKEN_PERMISSIONS/);

  rmSync(token);
  writeFileSync(token, 'b'.repeat(64), { mode: 0o600 });
  rmSync(token);
  symlinkSync(join(directory, 'missing-token-target'), token);
  assert.throws(() => gatewayToken(directory), /UNSAFE_TOKEN_PATH/);

  rmSync(token);
  writeFileSync(token, 'c'.repeat(64), { mode: 0o600 });
  const state = join(directory, 'state.json');
  rmSync(state, { force: true });
  symlinkSync(join(directory, 'missing-state-target'), state);
  assert.throws(() => new SessionStore(directory).write({ linked: true, connect: true, loggedOut: false }), /UNSAFE_SESSION_STORAGE_PATH/);
});