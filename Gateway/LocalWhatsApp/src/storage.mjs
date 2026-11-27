import { mkdirSync, readFileSync, writeFileSync, renameSync, lstatSync, existsSync, chmodSync, openSync, closeSync, unlinkSync } from 'node:fs';
import { join, resolve, isAbsolute } from 'node:path';
import { randomBytes } from 'node:crypto';
import { execFileSync } from 'node:child_process';

export function dataDirectory() {
  if (process.platform === 'win32') {
    if (!process.env.LOCALAPPDATA) throw new Error('WINDOWS_STORAGE_REQUIRED');
    return join(process.env.LOCALAPPDATA, 'LUMAR', 'LocalWhatsAppGateway');
  }

  if (process.platform !== 'linux') throw new Error('UNSUPPORTED_STORAGE_PLATFORM');
  const directory = process.env.LUMAR_LOCAL_WHATSAPP_DATA_DIRECTORY;
  if (!directory || !isAbsolute(directory)) throw new Error('LINUX_STORAGE_DIRECTORY_REQUIRED');
  const uid = process.geteuid?.();
  if (typeof uid === 'number' && uid === 0) throw new Error('LINUX_GATEWAY_ROOT_FORBIDDEN');

  const resolved = resolve(directory);
  const gatewayRoot = resolve(process.cwd());
  if (resolved === gatewayRoot || resolved.startsWith(gatewayRoot + '/')) {
    throw new Error('LINUX_STORAGE_DIRECTORY_UNSAFE');
  }
  return resolved;
}

export function validateLinuxStorageDirectory(directory, expectedUid = process.getuid?.()) {
  if (process.platform !== 'linux') return;
  if (!isAbsolute(directory)) throw new Error('LINUX_STORAGE_DIRECTORY_REQUIRED');

  const uid = process.geteuid?.();
  if (typeof uid === 'number' && uid === 0) throw new Error('LINUX_GATEWAY_ROOT_FORBIDDEN');

  let info;
  try {
    info = lstatSync(directory);
  } catch (error) {
    if (error?.code === 'ENOENT') throw new Error('LINUX_STORAGE_DIRECTORY_REQUIRED');
    throw error;
  }
  if (info.isSymbolicLink()) throw new Error('UNSAFE_STORAGE_PATH');
  if (!info.isDirectory()) throw new Error('LINUX_STORAGE_DIRECTORY_UNSAFE');
  if (typeof expectedUid !== 'number' || typeof info.uid !== 'number') {
    throw new TypeError('LINUX_STORAGE_OWNERSHIP_UNAVAILABLE');
  }
  if (info.uid !== expectedUid) throw new Error('LINUX_STORAGE_OWNERSHIP_INVALID');

  const mode = info.mode & 0o777;
  if ((mode & 0o077) !== 0 || (mode & 0o700) !== 0o700) {
    throw new Error('LINUX_STORAGE_PERMISSIONS_UNSAFE');
  }
}

function lstatIfExists(path) {
  try {
    return lstatSync(path);
  } catch (error) {
    if (error?.code === 'ENOENT') return null;
    throw error;
  }
}

function validateLinuxSensitiveDirectory(path) {
  const info = lstatIfExists(path);
  if (!info) return;
  if (info.isSymbolicLink() || !info.isDirectory()) throw new Error('UNSAFE_SESSION_STORAGE_PATH');
  if (info.uid !== process.getuid?.()) throw new Error('LINUX_STORAGE_OWNERSHIP_INVALID');
  if ((info.mode & 0o777) !== 0o700) throw new Error('LINUX_STORAGE_PERMISSIONS_UNSAFE');
}

function validateLinuxSensitiveFile(path, tokenFile = false) {
  const info = lstatIfExists(path);
  if (!info) return;
  if (info.isSymbolicLink()) throw new Error(tokenFile ? 'UNSAFE_TOKEN_PATH' : 'UNSAFE_SESSION_STORAGE_PATH');
  if (!info.isFile()) throw new Error(tokenFile ? 'UNSAFE_TOKEN_PATH' : 'UNSAFE_SESSION_STORAGE_PATH');
  if (info.uid !== process.getuid?.()) throw new Error(tokenFile ? 'UNSAFE_TOKEN_PATH' : 'LINUX_STORAGE_OWNERSHIP_INVALID');
  if ((info.mode & 0o777) !== 0o600) throw new Error(tokenFile ? 'UNSAFE_TOKEN_PERMISSIONS' : 'LINUX_STORAGE_PERMISSIONS_UNSAFE');
}

export function protectDirectory(directory) {
  if (!isAbsolute(directory)) throw new Error('UNSAFE_STORAGE_PATH');
  if (process.platform === 'linux') {
    const uid = process.geteuid?.();
    if (typeof uid === 'number' && uid === 0) throw new Error('LINUX_GATEWAY_ROOT_FORBIDDEN');
  }
  mkdirSync(directory, { recursive: true, mode: 0o700 });
  if (lstatSync(directory).isSymbolicLink()) throw new Error('UNSAFE_STORAGE_PATH');

  if (process.platform !== 'win32') {
    if (process.platform !== 'linux') throw new Error('UNSUPPORTED_STORAGE_PLATFORM');
    validateLinuxStorageDirectory(directory);
    chmodSync(directory, 0o700);

    validateLinuxSensitiveDirectory(join(directory, 'auth'));
    validateLinuxSensitiveDirectory(join(directory, 'web-cache'));
    validateLinuxSensitiveFile(join(directory, 'state.json'));
    validateLinuxSensitiveFile(join(directory, 'gateway-token'), true);
    return;
  }

  const script = `$ErrorActionPreference='Stop'; $path=$env:LUMAR_ACL_TARGET; `
    + `$identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().User; `
    + `$acl=New-Object System.Security.AccessControl.DirectorySecurity; `
    + `$acl.SetAccessRuleProtection($true,$false); $acl.SetOwner($identity); `
    + `foreach($sid in @($identity,(New-Object System.Security.Principal.SecurityIdentifier 'S-1-5-18'))) { `
    + `$rule=New-Object System.Security.AccessControl.FileSystemAccessRule($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'); $acl.AddAccessRule($rule) }; `
    + `[System.IO.Directory]::SetAccessControl($path,$acl); `
    + `$actual=Get-Acl -LiteralPath $path; if(-not $actual.AreAccessRulesProtected){throw 'ACL_NOT_PROTECTED'}; `
    + `$rules=$actual.GetAccessRules($true,$true,[System.Security.Principal.SecurityIdentifier]); `
    + `foreach($rule in $rules){if($rule.IdentityReference.Value -notin @($identity.Value,'S-1-5-18')){throw 'UNEXPECTED_ACCESS'}}; Write-Output 'ACL_VERIFIED'`;
  const result = execFileSync(join(process.env.SystemRoot, 'System32/WindowsPowerShell/v1.0/powershell.exe'),
    ['-NoProfile', '-NonInteractive', '-Command', script],
    { encoding: 'utf8', env: { ...process.env, LUMAR_ACL_TARGET: resolve(directory) }, windowsHide: true, timeout: 15000 });
  if (!result.includes('ACL_VERIFIED')) throw new Error('ACL_NOT_VERIFIED');
}

export class SessionStore {
  constructor(directory) {
    this.directory = directory;
    this.path = join(directory, 'state.json');
  }

  read() {
    let stateExists = existsSync(this.path);
    if (process.platform === 'linux') {
      validateLinuxSensitiveFile(this.path);
      stateExists = lstatIfExists(this.path) !== null;
    }
    if (!stateExists) return { linked: false, connect: false, loggedOut: false };
    const state = JSON.parse(readFileSync(this.path, 'utf8'));
    if (['linked', 'connect', 'loggedOut'].some(key => typeof state[key] !== 'boolean')) throw new Error('INVALID_SESSION_STATE');
    const providerIdsByCustomerMessageId = state.providerIdsByCustomerMessageId && typeof state.providerIdsByCustomerMessageId === 'object'
      ? state.providerIdsByCustomerMessageId
      : {};
    const ackByProviderId = state.ackByProviderId && typeof state.ackByProviderId === 'object'
      ? state.ackByProviderId
      : {};
    const result = { linked: state.linked, connect: state.connect, loggedOut: state.loggedOut };
    if (Object.keys(providerIdsByCustomerMessageId).length > 0)
      result.providerIdsByCustomerMessageId = providerIdsByCustomerMessageId;
    if (Object.keys(ackByProviderId).length > 0)
      result.ackByProviderId = ackByProviderId;
    return result;
  }

  write(value) {
    if (process.platform === 'linux') validateLinuxSensitiveFile(this.path);
    const temporary = join(this.directory, `state-${randomBytes(8).toString('hex')}.tmp`);
    writeFileSync(temporary, JSON.stringify(value), { flag: 'wx', mode: 0o600 });
    if (process.platform === 'linux') chmodSync(temporary, 0o600);
    renameSync(temporary, this.path);
  }
}

function readLockOwner(path) {
  try {
    if (lstatSync(path).isSymbolicLink()) throw new Error('UNSAFE_INSTANCE_LOCK');
    return Number(readFileSync(path, 'utf8').trim());
  } catch (error) {
    if (error?.message === 'UNSAFE_INSTANCE_LOCK') throw error;
    return null;
  }
}

function processIsRunning(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function removeStaleLock(path) {
  try { unlinkSync(path); } catch {}
}

export function acquireInstanceLock(directory) {
  if (process.platform !== 'linux') return { close() {} };

  const path = join(directory, '.gateway-instance.lock');
  try {
    const fd = openSync(path, 'wx', 0o600);
    writeFileSync(fd, `${process.pid}\n`, { encoding: 'utf8' });
    return {
      close() {
        closeSync(fd);
        if (readLockOwner(path) === process.pid) removeStaleLock(path);
      }
    };
  } catch (error) {
    if (error?.code !== 'EEXIST') throw error;
  }

  if (processIsRunning(readLockOwner(path))) throw new Error('INSTANCE_LOCKED');
  removeStaleLock(path);

  try {
    const fd = openSync(path, 'wx', 0o600);
    writeFileSync(fd, `${process.pid}\n`, { encoding: 'utf8' });
    return {
      close() {
        closeSync(fd);
        if (readLockOwner(path) === process.pid) removeStaleLock(path);
      }
    };
  } catch (error) {
    if (error?.code === 'EEXIST') throw new Error('INSTANCE_LOCKED');
    throw error;
  }
}

export function gatewayToken(directory, create = false) {
  const path = join(directory, 'gateway-token');
  if (process.platform === 'linux') {
    if (lstatSync(directory).isSymbolicLink()) throw new Error('UNSAFE_STORAGE_PATH');
  }
  if (process.platform === 'linux') {
    validateLinuxSensitiveFile(path, true);
  }
  if (!existsSync(path) && create) {
    writeFileSync(path, randomBytes(32).toString('hex'), { flag: 'wx', mode: 0o600 });
    if (process.platform === 'linux') chmodSync(path, 0o600);
  }
  if (lstatSync(path).isSymbolicLink()) throw new Error('UNSAFE_TOKEN_PATH');
  const token = readFileSync(path, 'utf8').trim();
  if (!/^[a-f0-9]{64}$/.test(token)) throw new Error('INVALID_GATEWAY_TOKEN');
  return token;
}