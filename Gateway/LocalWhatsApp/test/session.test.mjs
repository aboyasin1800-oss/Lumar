import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { SessionManager } from '../src/session.mjs';

function fixture(saved = { linked: false, connect: false }) {
  const drivers = [];
  const store = { read: () => ({ ...saved }), write: value => { saved = { ...value }; } };
  const factory = () => {
    const driver = new EventEmitter();
    driver.initialize = () => {};
    driver.destroy = async () => { driver.destroyed = true; };
    driver.logout = async () => { driver.loggedOut = true; };
    driver.send = async message => { driver.message = message; return 'real-transport-id'; };
    drivers.push(driver);
    return driver;
  };
  return { manager: new SessionManager(factory, store), drivers, store, factory };
}

test('link exposes temporary QR only through material, never status', async () => {
  const { manager, drivers } = fixture();
  assert.equal(manager.status().state, 'NotLinked');
  await manager.command('link');
  drivers[0].emit('qr', 'sensitive-temporary-qr');
  assert.equal(manager.linkMaterial().kind, 'qr');
  assert.equal(JSON.stringify(manager.status()).includes('sensitive'), false);
  manager.pairing.expiresAt = Date.now() - 1;
  assert.equal(manager.linkMaterial().value, null);
  drivers[0].emit('ready', '967777123456');
  assert.equal(manager.status().state, 'Connected');
  assert.equal(manager.status().currentNumber, '967777123456');
  assert.equal(manager.status().operationalActivation, false);
  assert.equal(manager.linkMaterial().value, null);
});

test('disconnect retains persisted session and reconnect uses it', async () => {
  const { manager, drivers } = fixture();
  await manager.command('link');
  drivers[0].emit('ready', '967777123456');
  await manager.command('disconnect');
  assert.equal(manager.status().linked, true);
  assert.equal(manager.status().state, 'Disconnected');
  assert.equal(manager.status().currentNumber, null);
  assert.equal(drivers[0].destroyed, true);
  await manager.command('reconnect');
  assert.equal(manager.status().state, 'Reconnecting');
  drivers[0].emit('ready', '967777999999');
  assert.equal(manager.status().currentNumber, null);
  drivers[1].emit('ready', '967777123456');
  assert.equal(manager.status().currentNumber, '967777123456');
});

test('restart restores intent without sending; intentional disconnect stays disconnected', async () => {
  const { manager, drivers, factory, store } = fixture();
  await manager.command('link');
  drivers[0].emit('ready', '967777123456');
  await manager.stop();
  const restored = new SessionManager(factory, store);
  await restored.restore();
  assert.equal(restored.status().state, 'Reconnecting');
  assert.equal(drivers.every(driver => !driver.message), true);
  await restored.command('disconnect');
  const paused = new SessionManager(factory, store);
  await paused.restore();
  assert.equal(paused.status().state, 'Disconnected');
  assert.equal(drivers.length, 2);
});

test('replace logs out old account, clears number and accepts only newly proven number', async () => {
  const { manager, drivers } = fixture();
  await manager.command('link');
  drivers[0].emit('ready', '967777123456');
  await manager.command('replace');
  assert.equal(drivers[0].loggedOut, true);
  assert.equal(manager.status().state, 'Linking');
  assert.equal(manager.status().currentNumber, null);
  drivers[1].emit('ready', '967777654321');
  assert.equal(manager.status().currentNumber, '967777654321');
  await manager.command('logout');
  assert.equal(manager.status().state, 'LoggedOut');
  assert.equal(manager.status().linked, false);
});

test('offline logout cannot pretend to revoke the account', async () => {
  const { manager } = fixture({ linked: true, connect: false });
  await assert.rejects(manager.command('logout'), { code: 'RECONNECT_REQUIRED' });
  await assert.rejects(manager.command('replace'), { code: 'RECONNECT_REQUIRED' });
});

test('errors never expose raw library data, and unknown send is not retried', async () => {
  const { manager, drivers } = fixture();
  await assert.rejects(manager.send({ text: 'final' }), { code: 'NOT_CONNECTED' });
  await manager.command('link');
  drivers[0].emit('failure', new Error('secret credentials'));
  assert.equal(manager.status().lastError, 'SESSION_CONNECTION_FAILED');
  drivers[0].emit('ready', '967777123456');
  drivers[0].send = async () => { throw new Error('secret phone'); };
  await assert.rejects(manager.send({ text: 'final' }), { code: 'SEND_UNCONFIRMED' });
  assert.equal(manager.status().lastError, 'SEND_UNCONFIRMED');
});

test('concurrent commands are rejected while sending, not queued', async () => {
  const { manager, drivers } = fixture();
  await manager.command('link');
  drivers[0].emit('ready', '967777123456');
  let complete;
  drivers[0].send = () => new Promise(resolve => { complete = resolve; });
  const sending = manager.send({ text: 'final' });
  await assert.rejects(manager.command('replace'), { code: 'BUSY' });
  await assert.rejects(manager.send({ text: 'other' }), { code: 'BUSY' });
  complete('provider-id');
  assert.deepEqual(await sending, { outcome: 'Sent', providerMessageId: 'provider-id', retryable: false });
});