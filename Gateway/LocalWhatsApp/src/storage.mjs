import { mkdirSync, readFileSync, writeFileSync, renameSync, lstatSync, existsSync } from 'node:fs';
import { join, resolve, isAbsolute } from 'node:path';
import { randomBytes } from 'node:crypto';
import { execFileSync } from 'node:child_process';

export function dataDirectory() {
  if (process.platform !== 'win32' || !process.env.LOCALAPPDATA) throw new Error('WINDOWS_STORAGE_REQUIRED');
  return join(process.env.LOCALAPPDATA, 'LUMAR', 'LocalWhatsAppGateway');
}

export function protectDirectory(directory) {
  if (process.platform !== 'win32' || !isAbsolute(directory)) throw new Error('WINDOWS_STORAGE_REQUIRED');
  mkdirSync(directory, { recursive: true });
  if (lstatSync(directory).isSymbolicLink()) throw new Error('UNSAFE_STORAGE_PATH');
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
    if (!existsSync(this.path)) return { linked: false, connect: false, loggedOut: false };
    const state = JSON.parse(readFileSync(this.path, 'utf8'));
    if (['linked', 'connect', 'loggedOut'].some(key => typeof state[key] !== 'boolean')) throw new Error('INVALID_SESSION_STATE');
    return { linked: state.linked, connect: state.connect, loggedOut: state.loggedOut };
  }

  write(value) {
    const temporary = join(this.directory, `state-${randomBytes(8).toString('hex')}.tmp`);
    writeFileSync(temporary, JSON.stringify(value), { flag: 'wx', mode: 0o600 });
    renameSync(temporary, this.path);
  }
}

export function gatewayToken(directory, create = false) {
  const path = join(directory, 'gateway-token');
  if (!existsSync(path) && create) writeFileSync(path, randomBytes(32).toString('hex'), { flag: 'wx', mode: 0o600 });
  if (lstatSync(path).isSymbolicLink()) throw new Error('UNSAFE_TOKEN_PATH');
  const token = readFileSync(path, 'utf8').trim();
  if (!/^[a-f0-9]{64}$/.test(token)) throw new Error('INVALID_GATEWAY_TOKEN');
  return token;
}