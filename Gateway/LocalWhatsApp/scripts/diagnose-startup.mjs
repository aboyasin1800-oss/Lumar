import { spawnSync, fork } from 'node:child_process';
import { once } from 'node:events';
import { existsSync, readFileSync, statSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataDirectory } from '../src/storage.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const directory = dataDirectory();
const evidence = resolve(root, '../../artifacts/local-whatsapp-foundation');
const result = { capturedAt: new Date().toISOString(), node: process.version, executable: process.execPath,
  directory, configuredPort: process.env.LUMAR_LOCAL_GATEWAY_PORT ?? '5094',
  nodeOptionsPresent: Boolean(process.env.NODE_OPTIONS), chromium: {}, files: {} };
for (const name of ['state.json', 'gateway-token']) {
  const path = join(directory, name);
  const status = { exists: existsSync(path), readable: false, valid: false };
  try {
    const raw = readFileSync(path, 'utf8');
    status.readable = true;
    status.modified = statSync(path).mtime.toISOString();
    if (name === 'gateway-token') status.valid = /^[a-f0-9]{64}$/.test(raw.trim());
    else {
      const state = JSON.parse(raw);
      status.valid = ['linked', 'connect', 'loggedOut'].every(key => typeof state[key] === 'boolean');
      if (status.valid) status.state = { linked: state.linked, connect: state.connect, loggedOut: state.loggedOut };
    }
  } catch (error) { status.error = ['ENOENT', 'EACCES', 'EPERM'].includes(error.code) ? error.code : 'READ_OR_FORMAT_FAILED'; }
  result.files[name] = status;
}
try {
  const puppeteer = (await import('puppeteer')).default;
  const executable = await puppeteer.executablePath();
  result.chromium = { dependencyLoadable: true, executableExists: existsSync(executable), executable };
} catch (error) {
  result.chromium = { dependencyLoadable: false, code: ['ERR_REQUIRE_ESM', 'ERR_MODULE_NOT_FOUND'].includes(error.code) ? error.code : 'DEPENDENCY_LOAD_FAILED' };
}
const script = `$ErrorActionPreference='Stop'; `
  + `$listeners=@(Get-NetTCPConnection -LocalPort $env:LUMAR_DIAG_PORT -State Listen -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,OwningProcess); `
  + `$processes=@(Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('node.exe','chrome.exe','chrome-headless-shell.exe') } | ForEach-Object { `
  + String.raw`[pscustomobject]@{Pid=$_.ProcessId;ParentPid=$_.ParentProcessId;Created=$_.CreationDate.ToUniversalTime().ToString('o');Name=$_.Name;Path=$_.ExecutablePath;GatewayScript=([regex]::Match($_.CommandLine,'(?:server|live-link|probe|diagnose-startup)\.mjs').Value);LumarProfile=($_.CommandLine -like '*LocalWhatsAppGateway*')} }); `
  + `$acl=$null; if(Test-Path -LiteralPath $env:LUMAR_DIAG_DIR){$actual=Get-Acl -LiteralPath $env:LUMAR_DIAG_DIR; $identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().User; `
  + `$unexpected=@($actual.GetAccessRules($true,$true,[System.Security.Principal.SecurityIdentifier]) | Where-Object {$_.IdentityReference.Value -notin @($identity.Value,'S-1-5-18')}); `
  + `$acl=[pscustomobject]@{InheritanceProtected=$actual.AreAccessRulesProtected;UnexpectedRuleCount=$unexpected.Count}}; `
  + `[pscustomobject]@{Listeners=$listeners;Processes=$processes;StorageAcl=$acl} | ConvertTo-Json -Depth 5 -Compress`;
const windows = spawnSync(join(process.env.SystemRoot, 'System32/WindowsPowerShell/v1.0/powershell.exe'),
  ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 20000,
    env: { ...process.env, LUMAR_DIAG_PORT: String(result.configuredPort), LUMAR_DIAG_DIR: directory } });
result.windowsInspectionExitCode = windows.status;
if (windows.status === 0) result.windows = JSON.parse(windows.stdout);
else result.windowsInspectionError = 'READ_ONLY_WINDOWS_INSPECTION_FAILED';
if (process.argv.includes('--probe-start')) {
  const child = fork(join(root, 'src/server.mjs'), [], { cwd: root, stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
    env: { ...process.env, LUMAR_LOCAL_TEST_RECIPIENT: '', LUMAR_LOCAL_MEDIA_ORIGIN: '' } });
  result.startAttempt = { pid: child.pid, ready: false };
  child.on('message', message => {
    if (message?.type === 'startup-failure') result.startAttempt.failure = { stage: message.stage, code: message.code };
  });
  let output = '';
  child.stdout.on('data', bytes => {
    output = (output + bytes.toString()).slice(-4096);
    if (output.includes('LUMAR Local Gateway listening')) {
      result.startAttempt.ready = true;
      child.send('shutdown');
    }
  });
  child.stderr.on('data', () => {});
  const [code] = await once(child, 'exit');
  result.startAttempt.exitCode = code;
}
mkdirSync(evidence, { recursive: true });
const filename = result.startAttempt ? 'startup-attempt.json' : 'startup-diagnostics.json';
writeFileSync(join(evidence, filename), JSON.stringify(result, null, 2));
console.log(JSON.stringify(result, null, 2));
const passed = windows.status === 0 && (!result.startAttempt || result.startAttempt.ready && result.startAttempt.exitCode === 0);
console.log(`EXIT_CODE=${passed ? 0 : 1}`);
if (!passed) process.exitCode = 1;