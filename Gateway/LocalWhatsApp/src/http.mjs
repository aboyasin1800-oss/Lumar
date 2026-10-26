import { createServer } from 'node:http';
import { timingSafeEqual, createHash } from 'node:crypto';
import { GatewayError } from './session.mjs';

const hash = value => createHash('sha256').update(value).digest();
const actions = new Set(['link', 'disconnect', 'logout', 'reconnect', 'replace']);

function normalizeYemenMobileNumber(phoneNumber) {
  if (typeof phoneNumber !== 'string') return null;
  const digitsOnly = phoneNumber.replace(/\D+/g, '');
  if (!digitsOnly) return null;
  const trimmed = digitsOnly.startsWith('00') ? digitsOnly.slice(2) : digitsOnly;
  if (trimmed.startsWith('967')) {
    const localPart = trimmed.slice(3);
    if (localPart.length === 9 && localPart.startsWith('7')) return `967${localPart}`;
    return null;
  }
  if (trimmed.length === 9 && trimmed.startsWith('7')) return `967${trimmed}`;
  return null;
}

async function jsonBody(request) {
  if (request.headers['content-type']?.split(';')[0] !== 'application/json') throw new GatewayError('INVALID_MESSAGE', 415);
  let size = 0;
  const chunks = [];
  for await (const chunk of request) {
    size += chunk.length;
    if (size > 32768) throw new GatewayError('INVALID_MESSAGE', 413);
    chunks.push(chunk);
  }
  try { return JSON.parse(Buffer.concat(chunks).toString('utf8')); }
  catch { throw new GatewayError('INVALID_MESSAGE', 400); }
}

async function loadMedia(imageUrl, allowedOrigin) {
  try {
    const url = new URL(imageUrl);
    if (!allowedOrigin || url.origin !== allowedOrigin || url.username || url.password
        || !['http:', 'https:'].includes(url.protocol)) throw new Error();
    const response = await fetch(url, { redirect: 'error', signal: AbortSignal.timeout(10000) });
    const mimeType = response.headers.get('content-type')?.split(';')[0];
    if (!response.ok || !['image/jpeg', 'image/png', 'image/webp'].includes(mimeType)) throw new Error();
    const chunks = [];
    let size = 0;
    for await (const chunk of response.body) {
      size += chunk.length;
      if (size > 5 * 1024 * 1024) throw new Error();
      chunks.push(chunk);
    }
    const bytes = Buffer.concat(chunks);
    const valid = mimeType === 'image/jpeg' ? bytes[0] === 0xff && bytes[1] === 0xd8
      : mimeType === 'image/png' ? bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))
        : bytes.toString('ascii', 0, 4) === 'RIFF' && bytes.toString('ascii', 8, 12) === 'WEBP';
    if (!valid) throw new Error();
    return { mimeType, base64: bytes.toString('base64') };
  } catch { throw new GatewayError('MEDIA_REJECTED', 400); }
}

function normalizeInternalMedia(bodyMedia) {
  if (!bodyMedia || typeof bodyMedia !== 'object' || Array.isArray(bodyMedia)) return null;
  const { mimeType, base64, filename } = bodyMedia;
  if (typeof mimeType !== 'string' || typeof base64 !== 'string' || typeof filename !== 'string') return null;
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(mimeType)) return null;
  const trimmed = filename.trim();
  if (!trimmed || trimmed.includes('..') || trimmed.includes('/') || trimmed.includes('\\')) return null;
  let bytes;
  try { bytes = Buffer.from(base64, 'base64'); }
  catch { return null; }
  if (bytes.length <= 0 || bytes.length > 5 * 1024 * 1024) return null;
  const valid = mimeType === 'image/jpeg' ? bytes[0] === 0xff && bytes[1] === 0xd8
    : mimeType === 'image/png' ? bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))
      : bytes.toString('ascii', 0, 4) === 'RIFF' && bytes.toString('ascii', 8, 12) === 'WEBP';
  if (!valid) return null;
  return { mimeType, base64: bytes.toString('base64'), filename: trimmed };
}

export function gatewayServer(manager, { token, testRecipient = '', mediaOrigin = '' }) {
  if (!/^[a-f0-9]{64}$/.test(token)) throw new Error('INVALID_GATEWAY_TOKEN');
  return createServer({ requestTimeout: 15000, headersTimeout: 10000, maxHeaderSize: 8192 }, async (request, response) => {
    response.setHeader('Cache-Control', 'no-store');
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Content-Type', 'application/json');
    const reply = (status, value) => { response.writeHead(status); response.end(JSON.stringify(value)); };
    if (request.headers.origin || !timingSafeEqual(hash(request.headers.authorization ?? ''), hash(`Bearer ${token}`))) {
      reply(401, { code: 'UNAUTHORIZED' });
      return;
    }
    try {
      const path = request.url;
      if (request.method === 'GET' && path === '/health') {
        reply(200, { healthy: true, contractVersion: 1, operationalActivation: false });
      } else if (request.method === 'GET' && path === '/session/status') {
        reply(200, manager.status());
      } else if (request.method === 'GET' && path === '/session/link-material') {
        reply(200, manager.linkMaterial());
      } else if (request.method === 'POST' && path?.startsWith('/session/') && actions.has(path.slice(9))) {
        const body = await jsonBody(request);
        if (!body || Array.isArray(body) || typeof body !== 'object' || Object.keys(body).length) throw new GatewayError('INVALID_MESSAGE', 400);
        reply(200, await manager.command(path.slice(9)));
      } else if (request.method === 'POST' && path === '/messages/send') {
        const body = await jsonBody(request);
        if (!body || typeof body !== 'object' || Array.isArray(body)
          || Object.keys(body).some(key => !['recipient', 'text', 'imageUrl', 'media'].includes(key))
          || typeof body.recipient !== 'string' || !/^[1-9][0-9]{7,14}$/.test(body.recipient)
          || typeof body.text !== 'string' || !body.text.trim() || body.text.length > 4096
          || (body.imageUrl != null && (typeof body.imageUrl !== 'string' || body.imageUrl.length > 2048))
          || (body.media != null && (typeof body.media !== 'object' || Array.isArray(body.media)))) {
          throw new GatewayError('INVALID_MESSAGE', 400);
        }
        const normalizedRecipient = normalizeYemenMobileNumber(body.recipient);
        const normalizedApproved = normalizeYemenMobileNumber(testRecipient);
        if (!normalizedRecipient || !normalizedApproved || normalizedRecipient !== normalizedApproved) {
          throw new GatewayError('TEST_RECIPIENT_REQUIRED', 403);
        }
        const media = normalizeInternalMedia(body.media) ?? (body.imageUrl ? await loadMedia(body.imageUrl, mediaOrigin) : null);
        reply(200, await manager.send({ recipient: body.recipient, text: body.text, media }));
      } else {
        reply(404, { code: 'NOT_FOUND' });
      }
    } catch (error) {
      const known = error instanceof GatewayError;
      reply(known ? error.status : 500, { code: known ? error.code : 'GATEWAY_ERROR',
        outcome: 'Failed', retryable: known && ['BUSY', 'NOT_CONNECTED'].includes(error.code) });
    }
  });
}