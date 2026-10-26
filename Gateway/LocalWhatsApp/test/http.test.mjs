import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { gatewayServer } from '../src/http.mjs';
import { SessionManager } from '../src/session.mjs';

const token = 'a'.repeat(64);

async function host(context, options = {}) {
  const manager = new SessionManager(() => { throw new Error('No live account in HTTP tests'); },
    { read: () => ({ linked: false, connect: false, loggedOut: false }), write: () => {} });
  const server = gatewayServer(manager, { token, ...options });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  context.after(() => new Promise(resolve => { server.closeAllConnections(); server.close(resolve); }));
  const url = `http://127.0.0.1:${server.address().port}`;
  const call = (path, body, headers = {}) => fetch(url + path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', ...headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return { call, url, manager };
}

test('every route requires a token including health; browser-origin requests are rejected', async context => {
  const { call } = await host(context);
  for (const route of ['/health', '/session/status', '/session/link-material', '/unknown']) {
    assert.equal((await call(route, undefined, { Authorization: '' })).status, 401);
    assert.equal((await call(route, undefined, { Origin: 'https://attacker.invalid' })).status, 401);
  }
  for (const action of ['link', 'disconnect', 'logout', 'reconnect', 'replace']) {
    assert.equal((await call(`/session/${action}`, {}, { Authorization: '' })).status, 401);
  }
  assert.equal((await call('/messages/send', {}, { Authorization: '' })).status, 401);
});

test('health is independent of WhatsApp and responses cannot be cached', async context => {
  const { call } = await host(context);
  const health = await call('/health');
  assert.equal(health.status, 200);
  assert.equal(health.headers.get('cache-control'), 'no-store');
  assert.equal((await health.json()).healthy, true);
  assert.equal((await (await call('/session/status')).json()).state, 'NotLinked');
  assert.equal((await (await call('/session/link-material')).json()).value, null);
});

test('foundation sending is closed by default and denies non-approved numbers', async context => {
  const { call } = await host(context);
  const response = await call('/messages/send', { recipient: '967777123456', text: 'final body' });
  assert.equal(response.status, 403);
  assert.equal((await response.json()).code, 'TEST_RECIPIENT_REQUIRED');
});

test('approved test recipient still requires connected session', async context => {
  const { call } = await host(context, { testRecipient: '967777123456' });
  const response = await call('/messages/send', { recipient: '967777123456', text: 'final body' });
  assert.equal(response.status, 503);
  assert.equal((await response.json()).code, 'NOT_CONNECTED');
  assert.equal((await call('/messages/send', { recipient: '967777999999', text: 'final body' })).status, 403);
});

test('invalid payloads, remote media and arbitrary administrative fields are rejected', async context => {
  const { call } = await host(context, { testRecipient: '967777123456' });
  for (const body of [null, [], {}, { recipient: 'group@g.us', text: 'x' }, { recipient: '967777123456', text: 'x', template: 'meta' }]) {
    assert.equal((await call('/messages/send', body)).status, 400);
  }
  assert.equal((await call('/session/replace', { currentNumber: 'fake' })).status, 400);
  const media = await call('/messages/send', { recipient: '967777123456', text: 'final', imageUrl: 'http://169.254.169.254/metadata' });
  assert.equal((await media.json()).code, 'MEDIA_REJECTED');
  assert.equal((await call('/messages/send', { recipient: '967777123456', text: 'x'.repeat(40000) })).status, 413);
});