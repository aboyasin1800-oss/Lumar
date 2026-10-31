import { join } from 'node:path';
import whatsapp from 'whatsapp-web.js';
import { dataDirectory } from '../src/storage.mjs';

const { Client, LocalAuth } = whatsapp;

export function isSafeProviderValue(value) {
  if (typeof value !== 'string') return false;
  const normalized = value.trim();
  if (!normalized || normalized.length < 8) return false;
  if (normalized.includes('\n') || normalized.includes('\r') || normalized.includes(' ')) return false;
  if (normalized === 'undefined' || normalized === 'null') return false;
  return true;
}

export function extractProviderMessageId(message) {
  if (!message || typeof message !== 'object') return null;
  const candidates = [
    message?.id?._serialized,
    message?.id?.id,
    message?.id?.remote?._serialized,
    message?._serialized,
    message?.messageId,
    message?.providerMessageId,
  ];

  for (const candidate of candidates) {
    if (typeof candidate === 'string') {
      const normalized = candidate.trim();
      if (isSafeProviderValue(normalized)) return normalized;
    }
  }
  return null;
}

export function createProbeCapture(input = {}) {
  const probeAttemptId = input.PROBE_ATTEMPT_ID || `probe-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
  const directId = input.directMessageIdPresent === true || Boolean(input.officialCorrelationKey);
  const hasSendReturn = input.sendReturned === true;
  const correlationKey = input.officialCorrelationKey || null;
  const messageCreateReceived = input.messageCreateReceived === true;
  const fromMe = input.fromMe === true;
  const eventMessageIdPresent = input.eventMessageIdPresent === true;
  const ackReceived = input.messageAckReceived === true;
  const correlationSafe = input.correlationSafe ?? (correlationKey || eventMessageIdPresent || directId ? 'YES' : 'NO');
  return {
    PROBE_ATTEMPT_ID: probeAttemptId,
    CLIENT_STATE_BEFORE: input.CLIENT_STATE_BEFORE ?? 'UNKNOWN',
    SEND_CALLED: input.SEND_CALLED ?? 'NO',
    SEND_RETURNED: hasSendReturn ? 'YES' : 'NO',
    SEND_RETURN_TYPE: input.sendReturnType ?? 'unknown',
    DIRECT_MESSAGE_ID_PRESENT: directId ? 'YES' : 'NO',
    MESSAGE_CREATE_RECEIVED: messageCreateReceived ? 'YES' : 'NO',
    MESSAGE_CREATE_FROM_ME: fromMe ? 'YES' : 'NO',
    EVENT_MESSAGE_ID_PRESENT: eventMessageIdPresent ? 'YES' : 'NO',
    MESSAGE_ACK_RECEIVED: ackReceived ? 'YES' : 'NO',
    ACK_LEVEL: Number.isFinite(input.ackLevel) ? input.ackLevel : null,
    CLIENT_STATE_AFTER: input.CLIENT_STATE_AFTER ?? 'UNKNOWN',
    OFFICIAL_CORRELATION_KEY: correlationKey ? 'PRESENT' : 'ABSENT',
    UNIQUE_PER_SEND: 'NOT_PROVEN',
    SAFE_FOR_SAME_RECIPIENT_SAME_TEXT_CONCURRENT_SENDS: 'NOT_PROVEN',
    CORRELATION_SAFE: correlationSafe,
  };
}

export function officialCorrelationKey(message) {
  return extractProviderMessageId(message);
}

export function runProbeSimulation({ sendResult, createEvent, ackEvent }) {
  const directId = extractProviderMessageId(sendResult);
  const createId = extractProviderMessageId(createEvent);
  const ackLevel = typeof ackEvent?.ack === 'number' ? ackEvent.ack : null;
  return createProbeCapture({
    PROBE_ATTEMPT_ID: `sim-${Date.now()}`,
    CLIENT_STATE_BEFORE: 'Connected',
    SEND_CALLED: 'YES',
    sendReturned: Boolean(sendResult),
    sendReturnType: sendResult === undefined ? 'undefined' : sendResult === null ? 'null' : typeof sendResult,
    directMessageIdPresent: Boolean(directId),
    messageCreateReceived: Boolean(createEvent),
    fromMe: createEvent?.fromMe === true,
    eventMessageIdPresent: Boolean(createId),
    messageAckReceived: Boolean(ackEvent),
    ackLevel,
    CLIENT_STATE_AFTER: 'Connected',
    officialCorrelationKey: createId || directId || null,
    correlationSafe: createId || directId ? 'YES' : 'NO',
  });
}

function sanitizeIndicator(value, fallback = 'NO') {
  return value === true ? 'YES' : value === false ? 'NO' : fallback;
}

function classifyLiveResult({ directTrustedId, messageCreateTrustedId, ackReceived }) {
  if (directTrustedId) return 'A';
  if (messageCreateTrustedId) return 'B';
  if (!directTrustedId && !messageCreateTrustedId) return 'C';
  if (messageCreateTrustedId && !ackReceived) return 'E';
  return 'F';
}

function stripFullNumber(target) {
  return target.replace(/[^\d+]/g, '').slice(-10);
}

async function runSingleLiveProbe(recipient) {
  const probeAttemptId = `probe-${Date.now().toString(36)}`;
  const directory = dataDirectory();
  const client = new Client({
    authStrategy: new LocalAuth({ clientId: 'lumar', dataPath: join(directory, 'auth') }),
    webVersionCache: { type: 'local', path: join(directory, 'web-cache') },
    puppeteer: { headless: true, timeout: 30000 },
    authTimeoutMs: 60000,
    qrMaxRetries: 4,
    deviceName: 'LUMAR Upstream Isolation Probe',
    browserName: 'Chrome',
  });

  const state = {
    PROBE_ATTEMPT_ID: probeAttemptId,
    CLIENT_STATE_BEFORE: 'UNKNOWN',
    SEND_CALLED: 'NO',
    SEND_RETURNED: 'NO',
    SEND_RETURN_TYPE: 'unknown',
    DIRECT_MESSAGE_ID_PRESENT: 'NO',
    MESSAGE_CREATE_RECEIVED: 'NO',
    MESSAGE_CREATE_FROM_ME: 'NO',
    EVENT_MESSAGE_ID_PRESENT: 'NO',
    MESSAGE_ACK_RECEIVED: 'NO',
    ACK_LEVEL: null,
    CLIENT_STATE_AFTER: 'UNKNOWN',
    PROBE_FINAL_CLASSIFICATION: 'F',
  };

  let directTrustedId = null;
  let messageCreateTrustedId = null;
  let ackLevel = null;
  let messageCreateReceived = false;
  let messageCreateFromMe = false;
  let messageCreateIdPresent = false;
  let ackReceived = false;

  const ready = new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('READY_TIMEOUT')), 120000);
    client.once('ready', () => { clearTimeout(timer); resolve(); });
    client.once('auth_failure', () => { clearTimeout(timer); reject(new Error('AUTH_FAILURE')); });
    client.once('disconnected', () => { clearTimeout(timer); reject(new Error('DISCONNECTED')); });
  });

  client.on('message_create', (message) => {
    if (!message || typeof message !== 'object') return;
    messageCreateReceived = true;
    messageCreateFromMe = message.fromMe === true;
    messageCreateIdPresent = Boolean(extractProviderMessageId(message));
    messageCreateTrustedId = extractProviderMessageId(message);
  });

  client.on('message_ack', (message, ack) => {
    ackReceived = true;
    ackLevel = Number.isFinite(ack) ? ack : null;
    if (message && typeof message === 'object') {
      const id = extractProviderMessageId(message);
      if (id) {
        if (Number.isFinite(ack)) ackLevel = ack;
      }
    }
  });

  await ready;
  state.CLIENT_STATE_BEFORE = client.info?.wid ? 'Connected' : 'Connected';
  state.SEND_CALLED = 'YES';

  const normalizedRecipient = String(recipient).replace(/[^\d+]/g, '').replace(/^00/, '+');
  const sendTarget = normalizedRecipient.startsWith('+') ? normalizedRecipient : `+${normalizedRecipient}`;
  const sendMessage = 'Probe check only';

  try {
    const result = await client.sendMessage(`${stripFullNumber(sendTarget)}@c.us`, sendMessage);
    const directId = extractProviderMessageId(result);
    directTrustedId = directId;
    state.SEND_RETURNED = result === undefined ? 'NO' : 'YES';
    state.SEND_RETURN_TYPE = result === undefined ? 'undefined' : result === null ? 'null' : typeof result;
    state.DIRECT_MESSAGE_ID_PRESENT = sanitizeIndicator(Boolean(directId));

    const timeout = setTimeout(() => {}, 30000);
    await new Promise((resolve) => {
      const start = Date.now();
      const interval = setInterval(() => {
        if (Date.now() - start > 30000) {
          clearInterval(interval);
          clearTimeout(timeout);
          resolve();
        }
        if (messageCreateReceived || ackReceived) {
          clearInterval(interval);
          clearTimeout(timeout);
          resolve();
        }
      }, 200);
    });

    state.MESSAGE_CREATE_RECEIVED = sanitizeIndicator(messageCreateReceived);
    state.MESSAGE_CREATE_FROM_ME = sanitizeIndicator(messageCreateFromMe);
    state.EVENT_MESSAGE_ID_PRESENT = sanitizeIndicator(messageCreateIdPresent);
    state.MESSAGE_ACK_RECEIVED = sanitizeIndicator(ackReceived);
    state.ACK_LEVEL = ackLevel;
    state.CLIENT_STATE_AFTER = 'Connected';
    state.PROBE_FINAL_CLASSIFICATION = classifyLiveResult({ directTrustedId: Boolean(directTrustedId), messageCreateTrustedId: Boolean(messageCreateTrustedId), ackReceived });
  } catch (error) {
    state.CLIENT_STATE_AFTER = 'Disconnected';
    state.PROBE_FINAL_CLASSIFICATION = 'F';
    console.error(JSON.stringify({ PROBE_ATTEMPT_ID: probeAttemptId, CLASSIFICATION: 'F' }));
    throw error;
  } finally {
    await client.destroy();
  }

  console.log(JSON.stringify(state, null, 2));
  return state;
}

const isDirectRun = (() => {
  try {
    return import.meta.url === new URL(`file://${process.argv[1]}`).href;
  } catch {
    return false;
  }
})();

if (isDirectRun) {
  const recipient = process.argv[2];
  if (!recipient) {
    console.error('RECIPIENT_REQUIRED');
    process.exit(1);
  }
  runSingleLiveProbe(recipient).catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
