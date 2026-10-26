import { fork } from 'node:child_process';
import { once } from 'node:events';
import { setTimeout as pause } from 'node:timers/promises';
import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataDirectory, gatewayToken } from '../src/storage.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const directory = dataDirectory();
const statePath = join(directory, 'state.json');
if (existsSync(statePath) && JSON.parse(readFileSync(statePath, 'utf8')).linked) throw new Error('PROBE_REFUSES_EXISTING_LINKED_ACCOUNT');
const evidence = resolve(root, '../../artifacts/local-whatsapp-foundation');
mkdirSync(evidence, { recursive: true });
const child = fork(join(root, 'src/server.mjs'), [], { cwd: root, stdio: ['ignore', 'pipe', 'pipe', 'ipc'] });
let output = '';
let readyResolve;
let readyReject;
let startupFailure;
const ready = new Promise((resolve, reject) => { readyResolve = resolve; readyReject = reject; });
const childExited = once(child, 'exit');
child.stdout.on('data', bytes => {
  output = (output + bytes.toString()).slice(-4096);
  if (output.includes('LUMAR Local Gateway listening')) readyResolve();
});
child.stderr.on('data', () => {});
child.on('message', message => {
  if (message?.type === 'startup-failure') startupFailure = { stage: message.stage, code: message.code };
});
child.once('exit', () => readyReject(new Error('GATEWAY_STARTUP_FAILED')));
let timeout;
const deadline = new Promise((_, reject) => { timeout = setTimeout(() => reject(new Error('GATEWAY_STARTUP_TIMEOUT')), 30000); });
const results = { startedAt: new Date().toISOString(), health: false, unauthenticatedRejected: false,
  linkContract: false, disconnect: false, shutdownConfirmed: false, operationalActivation: false, sendRequests: 0 };
try {
  await Promise.race([ready, deadline]);
  clearTimeout(timeout);
  const token = gatewayToken(directory);
  const port = process.env.LUMAR_LOCAL_GATEWAY_PORT ?? '5094';
  const base = `http://127.0.0.1:${port}`;
  const call = async (path, body) => {
    const response = await fetch(base + path, { method: body === undefined ? 'GET' : 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(35000) });
    if (!response.ok) throw new Error(`PROBE_HTTP_${response.status}`);
    return response.json();
  };
  results.health = (await call('/health')).healthy === true;
  results.unauthenticatedRejected = (await fetch(base + '/health')).status === 401;
  await call('/session/link', {});
  const linkDeadline = Date.now() + 140000;
  while (Date.now() < linkDeadline) {
    const status = await call('/session/status');
    if (status.state === 'Connected') throw new Error('UNEXPECTED_LINKED_ACCOUNT');
    if (status.state === 'Error') throw new Error('LIVE_LINK_CONTRACT_FAILED');
    const material = await call('/session/link-material');
    if (material.kind === 'qr' && typeof material.value === 'string' && Date.parse(material.expiresAt) > Date.now()) {
      results.linkContract = true;
      break;
    }
    await pause(1000);
  }
  results.disconnect = (await call('/session/disconnect', {})).state === 'NotLinked';
  if (!results.health || !results.unauthenticatedRejected || !results.linkContract || !results.disconnect) throw new Error('LIVE_PROBE_INCOMPLETE');
  results.exitCode = 0;
} catch (error) {
  console.error(error.message);
  if (startupFailure) console.error(JSON.stringify(startupFailure));
  results.exitCode = 1;
  results.failure = error.message;
  results.startupFailure = startupFailure;
  process.exitCode = 1;
} finally {
  clearTimeout(timeout);
  if (child.connected) child.send('shutdown');
  const [code] = await childExited;
  results.shutdownConfirmed = code === 0;
  if (!results.shutdownConfirmed) {
    results.exitCode = 1;
    process.exitCode = 1;
  }
  results.capturedAt = new Date().toISOString();
  writeFileSync(join(evidence, 'gateway-live-prelink.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results));
  console.log(`EXIT_CODE=${results.exitCode}`);
}