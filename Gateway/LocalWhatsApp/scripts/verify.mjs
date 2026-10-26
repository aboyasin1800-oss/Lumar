import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const evidence = resolve(root, '../../artifacts/local-whatsapp-foundation');
mkdirSync(evidence, { recursive: true });
function run(label, executable, args, cwd = root, env = process.env) {
  const result = spawnSync(executable, args, { cwd, env, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024, windowsHide: true });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}\nEXIT_CODE=${result.status}\n`;
  writeFileSync(join(evidence, `${label}.txt`), output);
  console.log(output);
  if (result.error || result.status !== 0) process.exit(result.status || 1);
}

if (process.argv.includes('--push-baseline')) {
  const workspace = resolve(root, '../..');
  run('push-baseline-unchanged', 'git.exe', ['diff', '--exit-code', 'HEAD', '--',
    'Backend/LUMAR_ERP_API_V2/Services/PushDeviceService.cs',
    'Backend/LUMAR_ERP_API_V2.Tests/MobilePushDeviceServiceTests.cs'], workspace);
  run('push-baseline-test', 'dotnet.exe', ['test', resolve(workspace, 'Backend/LUMAR_ERP_API_V2.Tests/LUMAR_ERP_API_V2.Tests.csproj'),
    '--no-build', '--filter', 'FullyQualifiedName~DispatchForOfficialNotification_UsesResolvedAccountWithoutClientTargetIds',
    '--tl:off', '--nologo', '-v', 'minimal', '--logger', 'trx;LogFileName=push-baseline.trx', '--results-directory', evidence]);
} else if (process.argv.includes('--backend')) {
  const command = `Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'LUMAR_ERP_API_V2*' -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'LUMAR_ERP_API_V2.dll') } | Select-Object ProcessId,Name,ExecutablePath | ConvertTo-Json -Compress`;
  const processes = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], { encoding: 'utf8', windowsHide: true });
  if (processes.status !== 0 || processes.stdout.trim()) {
    console.error('BACKEND_PROCESS_GUARD: database fixture tests require no operational backend dispatcher. No tests launched.');
    console.log(processes.stdout);
    process.exit(1);
  }
  const project = resolve(root, '../../Backend/LUMAR_ERP_API_V2.Tests/LUMAR_ERP_API_V2.Tests.csproj');
  const filter = ['LocalWhatsAppFoundationTests', 'MessageWriteServiceTests', 'WhatsAppCloudIntegrationTests',
    'WhatsAppConnectionSettingsTests', 'MetaTemplateContractTests', 'FirebasePushSenderTests',
    'MobilePushDeviceServiceTests'].map(name => `FullyQualifiedName~${name}`).join('|');
  run('backend-build', 'dotnet.exe', ['build', resolve(root, '../../Backend/LUMAR_ERP_API_V2/LUMAR_ERP_API_V2.csproj'), '--tl:off', '--nologo', '-v', 'minimal']);
  run('backend-regression', 'dotnet.exe', ['test', project, '--filter', filter, '--tl:off', '--nologo', '-v', 'minimal',
    '--logger', 'trx;LogFileName=regression.trx', '--results-directory', evidence], root,
    { ...process.env, Lumar__ConnectionString: 'Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True' });
} else {
  run('gateway-build', process.execPath, ['scripts/check.mjs']);
  const tests = readdirSync(join(root, 'test')).filter(name => name.endsWith('.test.mjs')).map(name => join('test', name));
  if (tests.length === 0) throw new Error('NO_TESTS');
  run('gateway-tests', process.execPath, ['--test', '--test-reporter=spec', '--test-reporter-destination=stdout',
    '--test-reporter=junit', `--test-reporter-destination=${join(evidence, 'gateway-tests.xml')}`, ...tests]);
  const npm = join(dirname(process.execPath), 'node_modules/npm/bin/npm-cli.js');
  const audit = spawnSync(process.execPath, [npm, 'audit', '--omit=dev', '--json'], { cwd: root, encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 });
  if (audit.error || !audit.stdout) throw new Error('DEPENDENCY_AUDIT_UNAVAILABLE');
  const report = JSON.parse(audit.stdout);
  writeFileSync(join(evidence, 'gateway-dependency-audit.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ dependencyAudit: report.metadata?.vulnerabilities, exitCode: audit.status }));
  if (!report.metadata || audit.status !== 0) process.exit(1);
}