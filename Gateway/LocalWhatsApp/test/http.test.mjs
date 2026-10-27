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

test('official customer recipients are accepted after local validation without test-recipient lock', async context => {
  const { call } = await host(context);
  const officialA = await call('/messages/send', { recipient: '967777123456', text: 'final body' });
  const officialB = await call('/messages/send', { recipient: '967775106545', text: 'final body' });

  assert.equal(officialA.status, 503);
  assert.equal((await officialA.json()).code, 'NOT_CONNECTED');
  assert.equal(officialB.status, 503);
  assert.equal((await officialB.json()).code, 'NOT_CONNECTED');
});

test('valid official customer numbers accept equivalent local and international formats; invalid or missing numbers fail safely', async context => {
  const { call } = await host(context);
  const localApproved = await call('/messages/send', { recipient: '775106545', text: 'final body' });
  const internationalApproved = await call('/messages/send', { recipient: '967775106545', text: 'final body' });
  const invalid = await call('/messages/send', { recipient: '967775106546x', text: 'final body' });
  const missing = await call('/messages/send', { recipient: '', text: 'final body' });
  const deleted = await call('/messages/send', { text: 'final body' });

  assert.equal(localApproved.status, 503);
  assert.equal((await localApproved.json()).code, 'NOT_CONNECTED');
  assert.equal(internationalApproved.status, 503);
  assert.equal((await internationalApproved.json()).code, 'NOT_CONNECTED');
  assert.equal(invalid.status, 400);
  assert.equal((await invalid.json()).code, 'INVALID_MESSAGE');
  assert.equal(missing.status, 400);
  assert.equal((await missing.json()).code, 'INVALID_MESSAGE');
  assert.equal(deleted.status, 400);
  assert.equal((await deleted.json()).code, 'INVALID_MESSAGE');
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

test('valid media payloads route to sendMessage without connection drops', async context => {
  const manager = {
    status: () => ({ state: 'Connected', currentNumber: '967777123456', lastError: null, linked: true, operationalActivation: false }),
    linkMaterial: () => ({ kind: null, value: null, expiresAt: null }),
    command: async () => ({ state: 'Connected', currentNumber: '967777123456', lastError: null, linked: true, operationalActivation: false }),
    send: async ({ recipient, text, media }) => {
      assert.equal(recipient, '967777123456');
      assert.equal(text, 'final body');
      assert.ok(media);
      assert.equal(media.mimeType, 'image/png');
      assert.equal(media.filename, 'valid.png');
      return { outcome: 'Sent', providerMessageId: 'provider-42', retryable: false };
    },
  };
  const server = gatewayServer(manager, { token, testRecipient: '967777123456' });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  context.after(() => new Promise(resolve => { server.closeAllConnections(); server.close(resolve); }));
  const port = server.address().port;
  const response = await fetch(`http://127.0.0.1:${port}/messages/send`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      recipient: '967777123456',
      text: 'final body',
      media: { mimeType: 'image/png', filename: 'valid.png', base64: Buffer.from([137,80,78,71,13,10,26,10,1,2,3,4]).toString('base64') },
    }),
  });
  assert.equal(response.status, 200);
  assert.equal((await response.json()).providerMessageId, 'provider-42');
});

test('oversized and malformed media produce 413/400 without abrupt socket close', async context => {
  const manager = {
    status: () => ({ state: 'Connected', currentNumber: '967777123456', lastError: null, linked: true, operationalActivation: false }),
    linkMaterial: () => ({ kind: null, value: null, expiresAt: null }),
    command: async () => ({ state: 'Connected', currentNumber: '967777123456', lastError: null, linked: true, operationalActivation: false }),
    send: async () => ({ outcome: 'Sent', providerMessageId: 'provider-42', retryable: false }),
  };
  const server = gatewayServer(manager, { token, testRecipient: '967777123456' });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  context.after(() => new Promise(resolve => { server.closeAllConnections(); server.close(resolve); }));
  const port = server.address().port;

  const oversized = await fetch(`http://127.0.0.1:${port}/messages/send`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      recipient: '967777123456',
      text: 'final body',
      media: {
        mimeType: 'image/png',
        filename: 'over.png',
        base64: Buffer.alloc(5 * 1024 * 1024 + 1, 0x00).toString('base64'),
      },
    }),
  });
  assert.equal(oversized.status, 413);
  assert.equal((await oversized.json()).code, 'INVALID_MESSAGE');

  const invalid = await fetch(`http://127.0.0.1:${port}/messages/send`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      recipient: '967777123456',
      text: 'final body',
      media: { mimeType: 'image/png', filename: 'broken.png', base64: 'not-base64!!!' },
    }),
  });
  assert.equal(invalid.status, 400);
  assert.equal((await invalid.json()).code, 'INVALID_MESSAGE');
});

test('pre-send internal failures return 500 and never masquerade as unconfirmed', async context => {
  const manager = {
    status: () => ({ state: 'Connected', currentNumber: '967777123456', lastError: null, linked: true, operationalActivation: false }),
    linkMaterial: () => ({ kind: null, value: null, expiresAt: null }),
    command: async () => ({ state: 'Connected', currentNumber: '967777123456', lastError: null, linked: true, operationalActivation: false }),
    send: async () => { throw new Error('internal send failure'); },
  };
  const server = gatewayServer(manager, { token, testRecipient: '967777123456' });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  context.after(() => new Promise(resolve => { server.closeAllConnections(); server.close(resolve); }));
  const port = server.address().port;
  const response = await fetch(`http://127.0.0.1:${port}/messages/send`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ recipient: '967777123456', text: 'final body' }),
  });
  assert.equal(response.status, 500);
  assert.equal((await response.json()).code, 'GATEWAY_ERROR');
});