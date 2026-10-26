import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const output = resolve(root, 'artifacts/local-whatsapp-foundation');
const files = [
  'Services/IOutboundMessageChannel.cs', 'Services/OutboundMessageDispatcher.cs',
  'Services/OutboundMessageBackgroundService.cs', 'Services/MessageWriteService.cs',
  'Services/WhatsAppCloudMessageChannel.cs', 'Services/InAppMessageChannel.cs',
  'Services/FirebasePushSender.cs', 'Services/PushDeviceService.cs',
  'Repositories/MessageRepository.cs', 'Repositories/SettingsRepository.cs',
  'Services/WhatsAppConnectionSettingsStore.cs',
];
const protectedFiles = {};
for (const file of files) {
  protectedFiles[file] = createHash('sha256').update(await readFile(resolve(root, 'Backend/LUMAR_ERP_API_V2', file))).digest('hex');
}
const libraries = {};
const dependencyOptions = process.argv.includes('--dependencies');
const packages = dependencyOptions ? ['puppeteer', 'extract-zip', 'basic-ftp', 'zod']
  : ['@whiskeysockets/baileys', 'whatsapp-web.js', '@wppconnect-team/wppconnect', '@wppconnect-team/wppconnect-server'];
for (const name of packages) {
  const response = await fetch(`https://registry.npmjs.org/${encodeURIComponent(name)}`, { signal: AbortSignal.timeout(30000) });
  if (response.status === 404) {
    libraries[name] = { registryStatus: 404, availableAsNpmPackage: false };
    continue;
  }
  if (!response.ok) throw new Error(`${name}: Registry status ${response.status}`);
  const registry = await response.json();
  const version = registry['dist-tags'].latest;
  const entry = registry.versions[version];
  libraries[name] = { version, tags: registry['dist-tags'], modified: registry.time.modified, engines: entry.engines,
    license: entry.license, repository: entry.repository, dependencies: entry.dependencies };
}
const storePath = process.env.LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH
  ?? join(process.env.LOCALAPPDATA, 'LUMAR_ERP_API_V2/WhatsAppSecrets/settings.json');
let metaStore = { present: false };
try {
  const bytes = await readFile(storePath);
  const store = JSON.parse(bytes);
  metaStore = { present: true, enabled: store.Enabled ?? null,
    phoneConfigured: Boolean(store.PhoneNumberId), sha256: createHash('sha256').update(bytes).digest('hex') };
} catch (error) {
  if (error.code !== 'ENOENT') throw new Error('Cannot audit Meta store safely.');
}
const program = await readFile(resolve(root, 'Backend/LUMAR_ERP_API_V2/Program.cs'), 'utf8');
const operationalAdapters = [...program.matchAll(/AddScoped<IOutboundMessageChannel,\s*([A-Za-z]+)>/g)].map(match => match[1]);
if (JSON.stringify(operationalAdapters) !== JSON.stringify(['InAppMessageChannel', 'WhatsAppCloudMessageChannel'])) throw new Error('OPERATIONAL_CHANNEL_REGISTRATION_CHANGED');
const result = { capturedAt: new Date().toISOString(), node: process.version, platform: process.platform,
  operationalMapping: 'WhatsApp -> WhatsAppCloudMessageChannel (Program.cs)', operationalAdapters, metaStore, protectedFiles, libraries };
if (process.argv.includes('--after')) {
  const before = JSON.parse(await readFile(join(output, 'audit-before.json'), 'utf8'));
  result.protectedFilesUnchanged = Object.keys(protectedFiles).every(file => before.protectedFiles[file] === protectedFiles[file]);
  result.metaStoreUnchanged = JSON.stringify(before.metaStore) === JSON.stringify(metaStore);
  if (!result.protectedFilesUnchanged || !result.metaStoreUnchanged) throw new Error('PROTECTED_SOURCE_CHANGED');
}
await mkdir(output, { recursive: true });
const target = dependencyOptions ? 'audit-dependency-options.json' : process.argv.includes('--after') ? 'audit-after.json' : 'audit-before.json';
await writeFile(join(output, target), JSON.stringify(result, null, 2), { flag: 'wx' });
console.log(JSON.stringify(result, null, 2));
console.log('EXIT_CODE=0');