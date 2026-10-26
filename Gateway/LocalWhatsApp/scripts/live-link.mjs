import { fork, spawn } from 'node:child_process';
import { once } from 'node:events';
import { setTimeout as pause } from 'node:timers/promises';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataDirectory, gatewayToken } from '../src/storage.mjs';

const recoveryOnly = process.argv.includes('--recovery-only');
if (!recoveryOnly && !process.stdout.isTTY) throw new Error('INTERACTIVE_OWNER_TERMINAL_REQUIRED');
const root = fileURLToPath(new URL('../', import.meta.url));
const evidence = resolve(root, recoveryOnly ? '../../artifacts/local-whatsapp-provider' : '../../artifacts/local-whatsapp-foundation');
const port = process.env.LUMAR_LOCAL_GATEWAY_PORT ?? '5094';
const endpoint = `http://127.0.0.1:${port}`;
const results = { connected: false, currentNumber: false, health: false, restart: false,
  sessionRecovery: false, newQrRequired: false, operationalActivation: false, sendRequests: 0 };
let child;
let owner;
let token;

async function bounded(promise, milliseconds, code) {
  let timer;
  try {
    return await Promise.race([promise, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error(code)), milliseconds);
    })]);
  } finally { clearTimeout(timer); }
}

async function startGateway() {
  child = fork(join(root, 'src/server.mjs'), [], {
    cwd: root, stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
    env: { ...process.env, LUMAR_LOCAL_TEST_RECIPIENT: '', LUMAR_LOCAL_MEDIA_ORIGIN: '' },
  });
  let output = '';
  await bounded(new Promise((resolve, reject) => {
    child.on('message', message => {
      const stages = ['INSTANCE_LOCK', 'STORAGE_ACCESS', 'SESSION_STATE', 'GATEWAY_TOKEN', 'HTTP_BIND', 'SESSION_RESTORE'];
      const codes = ['EADDRINUSE', 'EACCES', 'EPERM', 'ENOENT', 'ETIMEDOUT', 'UNSAFE_STORAGE_PATH',
        'INVALID_SESSION_STATE', 'UNSAFE_TOKEN_PATH', 'INVALID_GATEWAY_TOKEN', 'ACL_NOT_VERIFIED', 'STARTUP_OPERATION_FAILED'];
      if (message?.type === 'startup-failure' && stages.includes(message.stage) && codes.includes(message.code)) {
        results.startupFailure = { stage: message.stage, code: message.code };
      }
    });
    child.stdout.on('data', bytes => {
      output = (output + bytes.toString()).slice(-4096);
      if (output.includes('LUMAR Local Gateway listening')) resolve();
    });
    child.stderr.on('data', () => {});
    child.once('error', () => reject(new Error('GATEWAY_START_FAILED')));
    child.once('exit', () => reject(new Error('GATEWAY_START_FAILED')));
  }), 30000, 'GATEWAY_START_TIMEOUT');
  token = gatewayToken(dataDirectory());
}

async function stopGateway() {
  if (!child || child.exitCode !== null || child.signalCode !== null) return;
  const exited = once(child, 'exit');
  if (!child.connected) throw new Error('GATEWAY_CONTROL_UNAVAILABLE');
  child.send('shutdown');
  const [code] = await bounded(exited, 60000, 'GATEWAY_SHUTDOWN_UNCONFIRMED');
  if (code !== 0) throw new Error('GATEWAY_SHUTDOWN_UNCONFIRMED');
  child = null;
}

async function call(path) {
  const response = await fetch(endpoint + path, { headers: { Authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(15000) });
  if (!response.ok) throw new Error('GATEWAY_REQUEST_FAILED');
  return response.json();
}

async function connectedSession(recovery) {
  const deadline = Date.now() + 180000;
  while (Date.now() < deadline) {
    const status = await call('/session/status');
    if (status.operationalActivation !== false) throw new Error('ACTIVATION_CONTRACT_FAILED');
    if (status.state === 'Connected' && /^[1-9][0-9]{7,14}$/.test(status.currentNumber ?? '')) return status;
    if (recovery) {
      const material = await call('/session/link-material');
      if (material.kind === 'qr') {
        results.newQrRequired = true;
        throw new Error('RECOVERY_REQUIRES_NEW_QR');
      }
    }
    if (['Error', 'LoggedOut'].includes(status.state)) throw new Error('SESSION_NOT_CONNECTED');
    await pause(1000);
  }
  throw new Error('SESSION_CONNECTION_TIMEOUT');
}

try {
  await startGateway();
  const initial = await call('/session/status');
  if (!initial.linked) {
    if (recoveryOnly) throw new Error('EXISTING_LINK_REQUIRED');
    owner = spawn(process.execPath, [join(root, 'src/owner.mjs'), 'link'], { cwd: root, stdio: 'inherit' });
    const [code] = await once(owner, 'exit');
    owner = null;
    if (code !== 0) throw new Error('OWNER_LINK_NOT_COMPLETED');
  }
  const linked = await connectedSession(recoveryOnly);
  results.connected = true;
  results.currentNumber = true;
  results.maskedNumber = `***${linked.currentNumber.slice(-4)}`;
  results.health = (await call('/health')).healthy === true;
  console.log('تم إثبات الاتصال والرقم. جارٍ إعادة تشغيل البوابة للتحقق من استعادة الجلسة دون QR جديد.');
  await stopGateway();
  await startGateway();
  results.restart = true;
  const restored = await connectedSession(true);
  results.sessionRecovery = restored.currentNumber === linked.currentNumber;
  results.health = results.health && (await call('/health')).healthy === true;
  if (!results.sessionRecovery || !results.health) throw new Error('RECOVERY_VERIFICATION_FAILED');
  results.exitCode = 0;
  console.log('تم إثبات استعادة الجلسة والرقم نفسه. لم تُرسل أي رسالة. أبلغ سعد: اكتمل تحقق الربط والاستعادة.');
} catch (error) {
  const safeCodes = new Set(['GATEWAY_START_FAILED', 'GATEWAY_START_TIMEOUT', 'GATEWAY_SHUTDOWN_UNCONFIRMED',
    'GATEWAY_CONTROL_UNAVAILABLE', 'GATEWAY_REQUEST_FAILED', 'ACTIVATION_CONTRACT_FAILED',
    'RECOVERY_REQUIRES_NEW_QR', 'SESSION_NOT_CONNECTED', 'SESSION_CONNECTION_TIMEOUT',
    'OWNER_LINK_NOT_COMPLETED', 'RECOVERY_VERIFICATION_FAILED', 'EXISTING_LINK_REQUIRED']);
  results.failure = safeCodes.has(error.message) ? error.message : 'LIVE_VERIFICATION_FAILED';
  results.exitCode = 1;
  console.error(`تعذر استكمال التحقق: ${results.failure}. لا ترسل QR أو ملفات الجلسة إلى المحادثة.`);
  if (results.startupFailure) console.error(`مرحلة الفشل: ${results.startupFailure.stage}؛ الرمز: ${results.startupFailure.code}`);
  process.exitCode = 1;
} finally {
  try { await stopGateway(); }
  catch {
    results.shutdownConfirmed = false;
    results.exitCode = 1;
    process.exitCode = 1;
    console.error('لم يُؤكد إغلاق البوابة. لا تبدأ نسخة أخرى قبل مراجعة العملية التابعة.');
  }
  mkdirSync(evidence, { recursive: true });
  writeFileSync(join(evidence, 'gateway-live-link.json'), JSON.stringify({ ...results, capturedAt: new Date().toISOString() }, null, 2));
  console.log(`EXIT_CODE=${results.exitCode}`);
}