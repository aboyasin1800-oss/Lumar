import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const evidence = join(root, 'artifacts/local-whatsapp-provider');
mkdirSync(evidence, { recursive: true });
const env = { ...process.env,
  Lumar__ConnectionString: 'Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True' };

function run(label, executable, args, cwd = root) {
  console.log(`START ${label}`);
  const startedAt = new Date().toISOString();
  const result = spawnSync(executable, args, { cwd, env, encoding: 'utf8', windowsHide: true, maxBuffer: 32 * 1024 * 1024 });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}\nEXIT_CODE=${result.status}\n`;
  writeFileSync(join(evidence, `${label}.txt`), output);
  writeFileSync(join(evidence, `${label}-execution.json`), JSON.stringify({ startedAt, finishedAt: new Date().toISOString(), exitCode: result.status }, null, 2));
  console.log(output);
  if (result.error || result.status !== 0) process.exit(result.status || 1);
}

if (process.argv.includes('--audit')) {
  const baseline = JSON.parse(readFileSync(join(root, 'artifacts/local-whatsapp-foundation/audit-before.json'), 'utf8'));
  const authorized = new Set(['Services/MessageWriteService.cs', 'Services/OutboundMessageDispatcher.cs', 'Repositories/SettingsRepository.cs']);
  const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex');
  const files = Object.entries(baseline.protectedFiles).filter(([name]) => !authorized.has(name))
    .map(([name, before]) => ({ name, unchanged: hash(join(root, 'Backend/LUMAR_ERP_API_V2', name)) === before }));
  const metaStoreUnchanged = hash(join(process.env.LOCALAPPDATA, 'LUMAR_ERP_API_V2/WhatsAppSecrets/settings.json')) === baseline.metaStore.sha256;
  const result = { capturedAt: new Date().toISOString(), files, metaStoreUnchanged,
    passed: metaStoreUnchanged && files.every(file => file.unchanged) };
  writeFileSync(join(evidence, 'protected-contracts.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result, null, 2));
  process.exitCode = result.passed ? 0 : 1;
} else if (process.argv.includes('--windows')) {
  run('flutter-windows-build', 'cmd.exe', ['/d', '/c', 'flutter build windows --debug --dart-define=LUMAR_API_URL=http://127.0.0.1:5093'], join(root, 'frontend/tailoring_system'));
} else if (process.argv.includes('--flutter')) {
  const flutter = join(root, 'frontend/tailoring_system');
  run('flutter-tests', 'cmd.exe', ['/d', '/c', 'flutter test test/whatsapp_provider_management_test.dart --reporter expanded'], flutter);
  run('flutter-analyze', 'cmd.exe', ['/d', '/c', 'flutter analyze lib/screens/settings/whatsapp_provider_management_screen.dart lib/screens/settings_screen.dart test/whatsapp_provider_management_test.dart'], flutter);
} else if (process.argv.includes('--gateway')) {
  run('gateway-regression', 'cmd.exe', ['/d', '/c', 'npm run build'], join(root, 'Gateway/LocalWhatsApp'));
  run('gateway-tests', 'cmd.exe', ['/d', '/c', 'npm test'], join(root, 'Gateway/LocalWhatsApp'));
  run('gateway-dependency-audit', 'cmd.exe', ['/d', '/c', 'npm audit --omit=dev --json'], join(root, 'Gateway/LocalWhatsApp'));
} else {
  let filter = 'FullyQualifiedName~WhatsAppProviderIntegrationTests';
  if (process.argv.includes('--regression')) {
    const guard = spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command',
      "@(Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'LUMAR_ERP_API_V2*' -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'LUMAR_ERP_API_V2.dll') }).Count"], { encoding: 'utf8', windowsHide: true });
    if (guard.status !== 0 || guard.stdout.trim() !== '0') throw new Error('OPERATIONAL_BACKEND_MUST_BE_STOPPED_IN_VISUAL_STUDIO_BEFORE_DATABASE_TESTS');
    filter = ['WhatsAppProviderIntegrationTests', 'LocalWhatsAppFoundationTests', 'MessageWriteServiceTests',
      'WhatsAppCloudIntegrationTests', 'WhatsAppConnectionSettingsTests', 'MetaTemplateContractTests',
      'FirebasePushSenderTests', 'MobilePushDeviceServiceTests'].map(name => `FullyQualifiedName~${name}`).join('|');
    run('backend-build', 'dotnet.exe', ['build', resolve(root, 'Backend/LUMAR_ERP_API_V2/LUMAR_ERP_API_V2.csproj'), '--tl:off', '--nologo', '-v', 'minimal']);
  }
  const label = process.argv.includes('--regression') ? 'backend-regression' : 'backend-provider-tests';
  run(label, 'dotnet.exe', ['test', resolve(root, 'Backend/LUMAR_ERP_API_V2.Tests/LUMAR_ERP_API_V2.Tests.csproj'),
    '--filter', filter, '--tl:off', '--nologo', '-v', 'minimal', '--logger', `trx;LogFileName=${label}.trx`, '--results-directory', evidence]);
}