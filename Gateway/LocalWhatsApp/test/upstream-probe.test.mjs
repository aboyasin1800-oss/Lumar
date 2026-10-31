import test from 'node:test';
import assert from 'node:assert/strict';
import {
  extractProviderMessageId,
  officialCorrelationKey,
  isSafeProviderValue,
  createProbeCapture,
  runProbeSimulation,
} from '../scripts/upstream-isolation-probe.mjs';

test('direct message id is present', () => {
  const capture = createProbeCapture({
    sendReturned: true,
    sendReturnType: 'object',
    directMessageIdPresent: true,
  });
  assert.equal(capture.SEND_RETURNED, 'YES');
  assert.equal(capture.DIRECT_MESSAGE_ID_PRESENT, 'YES');
});

test('direct return undefined is captured without false positives', () => {
  const capture = createProbeCapture({
    sendReturned: false,
    sendReturnType: 'undefined',
    directMessageIdPresent: false,
  });
  assert.equal(capture.SEND_RETURNED, 'NO');
  assert.equal(capture.DIRECT_MESSAGE_ID_PRESENT, 'NO');
  assert.equal(capture.SEND_RETURN_TYPE, 'undefined');
});

test('message_create with trusted id is accepted and tracked', () => {
  const message = {
    fromMe: true,
    to: { _serialized: '967777123456@c.us' },
    body: 'hello',
    id: { fromMe: true, remote: { _serialized: '967777123456@c.us' }, id: 'msg-1', _serialized: 'wamid.create.1' },
  };
  assert.equal(extractProviderMessageId(message), 'wamid.create.1');
  assert.equal(officialCorrelationKey(message), 'wamid.create.1');
  assert.equal(isSafeProviderValue('wamid.create.1'), true);
});

test('message_create without id is rejected', () => {
  const message = { fromMe: true, body: 'hello', id: { fromMe: true } };
  assert.equal(extractProviderMessageId(message), null);
  const capture = createProbeCapture({
    messageCreateReceived: true,
    eventMessageIdPresent: false,
    fromMe: true,
  });
  assert.equal(capture.EVENT_MESSAGE_ID_PRESENT, 'NO');
});

test('ack arrives with trusted id and recorded level', () => {
  const ack = runProbeSimulation({
    sendResult: { id: { _serialized: 'wamid.ack.1', id: 'remote-ack-1' } },
    createEvent: { fromMe: true, id: { _serialized: 'wamid.ack.1', id: 'remote-ack-1' } },
    ackEvent: { id: { _serialized: 'wamid.ack.1', id: 'remote-ack-1' }, ack: 3 },
  });
  assert.equal(ack.MESSAGE_ACK_RECEIVED, 'YES');
  assert.equal(ack.ACK_LEVEL, 3);
});

test('ack missing is captured as no', () => {
  const ack = runProbeSimulation({
    sendResult: { id: { _serialized: 'wamid.noack.1', id: 'remote-noack-1' } },
    createEvent: { fromMe: true, id: { _serialized: 'wamid.noack.1', id: 'remote-noack-1' } },
    ackEvent: null,
  });
  assert.equal(ack.MESSAGE_ACK_RECEIVED, 'NO');
  assert.equal(ack.ACK_LEVEL, null);
});

test('fromMe=false event is filtered', () => {
  const result = runProbeSimulation({
    sendResult: { id: { _serialized: 'wamid.filter.1', id: 'remote-filter-1' } },
    createEvent: { fromMe: false, id: { _serialized: 'wamid.filter.1', id: 'remote-filter-1' } },
    ackEvent: null,
  });
  assert.equal(result.MESSAGE_CREATE_FROM_ME, 'NO');
  assert.equal(result.MESSAGE_CREATE_RECEIVED, 'YES');
});

test('late event is still captured but not correlated as current send', () => {
  const result = runProbeSimulation({
    sendResult: { id: { _serialized: 'wamid.late.1', id: 'remote-late-1' } },
    createEvent: { fromMe: true, id: { _serialized: 'wamid.late.1', id: 'remote-late-1' }, delayed: true },
    ackEvent: null,
  });
  assert.equal(result.MESSAGE_CREATE_RECEIVED, 'YES');
  assert.equal(result.CORRELATION_SAFE, 'YES');
});

test('concurrent sends do not share the same safe correlation key', () => {
  const firstEvent = { fromMe: true, id: { _serialized: 'wamid.concurrent.a', id: 'remote-a' }, recipient: '967777123456@c.us', text: 'text-a' };
  const secondEvent = { fromMe: true, id: { _serialized: 'wamid.concurrent.b', id: 'remote-b' }, recipient: '967777123456@c.us', text: 'text-b' };
  assert.notEqual(officialCorrelationKey(firstEvent), officialCorrelationKey(secondEvent));
  const first = runProbeSimulation({ sendResult: { id: { _serialized: 'wamid.concurrent.a', id: 'remote-a' } }, createEvent: firstEvent, ackEvent: null });
  const second = runProbeSimulation({ sendResult: { id: { _serialized: 'wamid.concurrent.b', id: 'remote-b' } }, createEvent: secondEvent, ackEvent: null });
  assert.equal(first.CORRELATION_SAFE, 'YES');
  assert.equal(second.CORRELATION_SAFE, 'YES');
});

test('sensitive data is not exposed in the capture', () => {
  const capture = createProbeCapture({
    messageCreateReceived: true,
    eventMessageIdPresent: true,
    officialCorrelationKey: 'wamid.redacted',
    secrets: 'token-hidden',
  });
  assert.equal(JSON.stringify(capture).includes('token'), false);
  assert.equal(JSON.stringify(capture).includes('secret'), false);
  assert.equal(JSON.stringify(capture).includes('wamid.redacted'), false);
  assert.equal(capture.OFFICIAL_CORRELATION_KEY, 'PRESENT');
});
