import { readdir } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
for (const directory of ['src', 'test', 'scripts']) {
  for (const name of await readdir(join(root, directory))) {
    if (!name.endsWith('.mjs')) continue;
    const checked = spawnSync(process.execPath, ['--check', join(root, directory, name)], { stdio: 'inherit' });
    if (checked.status !== 0) process.exit(checked.status ?? 1);
  }
}
console.log('Gateway Build EXIT_CODE=0');