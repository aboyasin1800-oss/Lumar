import { createServer as createLock } from 'node:net';
import { createHash } from 'node:crypto';
import { once } from 'node:events';
import { dataDirectory, protectDirectory, SessionStore, gatewayToken } from './storage.mjs';
import { SessionManager } from './session.mjs';
import { WebDriver } from './web-driver.mjs';
import { gatewayServer } from './http.mjs';

const port = Number(process.env.LUMAR_LOCAL_GATEWAY_PORT ?? 5094);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('INVALID_GATEWAY_PORT');
const directory = dataDirectory();
const lock = createLock(socket => socket.destroy());
const pipe = String.raw`\\.\pipe\lumar-wa-${createHash('sha256').update(directory.toLowerCase()).digest('hex').slice(0, 24)}`;
let manager;
let server;
let stopping = false;
let startupStage = 'INSTANCE_LOCK';

async function shutdown() {
  if (stopping) return;
  stopping = true;
  if (server) {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
  }
  try { await manager?.stop(); }
  catch { console.error('GATEWAY_SHUTDOWN_UNCONFIRMED'); process.exitCode = 1; }
  await new Promise(resolve => lock.close(resolve));
}

try {
  lock.listen(pipe);
  await once(lock, 'listening');
  startupStage = 'STORAGE_ACCESS';
  protectDirectory(directory);
  startupStage = 'SESSION_STATE';
  manager = new SessionManager(() => new WebDriver(directory), new SessionStore(directory));
  startupStage = 'GATEWAY_TOKEN';
  const mediaOrigin = process.env.LUMAR_CAMPAIGN_MEDIA_PUBLIC_BASE_URL
    || process.env.LUMAR_PUBLIC_BACKEND_BASE_URL
    || process.env.LUMAR_LOCAL_MEDIA_ORIGIN
    || '';
  server = gatewayServer(manager, { token: gatewayToken(directory, true),
    testRecipient: process.env.LUMAR_LOCAL_TEST_RECIPIENT ?? '',
    mediaOrigin: mediaOrigin.replace(/\/$/, '') });
  startupStage = 'HTTP_BIND';
  server.listen(port, '127.0.0.1');
  await once(server, 'listening');
  process.on('SIGINT', () => { void shutdown(); });
  process.on('SIGTERM', () => { void shutdown(); });
  process.on('message', message => {
    if (message === 'shutdown') {
      void shutdown().finally(() => { if (process.connected) process.disconnect(); });
    }
  });
  startupStage = 'SESSION_RESTORE';
  await manager.restore();
  console.log(`LUMAR Local Gateway listening at http://127.0.0.1:${port}; operationalActivation=false`);
} catch (error) {
  const allowedCodes = new Set(['EADDRINUSE', 'EACCES', 'EPERM', 'ENOENT', 'ETIMEDOUT',
    'UNSAFE_STORAGE_PATH', 'INVALID_SESSION_STATE', 'UNSAFE_TOKEN_PATH', 'INVALID_GATEWAY_TOKEN', 'ACL_NOT_VERIFIED']);
  let code = 'STARTUP_OPERATION_FAILED';
  if (allowedCodes.has(error.code)) code = error.code;
  else if (allowedCodes.has(error.message)) code = error.message;
  const failure = { type: 'startup-failure', stage: startupStage, code };
  if (process.connected) process.send(failure);
  console.error(JSON.stringify(failure));
  await shutdown();
  process.exitCode = 1;
}