import test from 'node:test';
import assert from 'node:assert/strict';
import puppeteer from 'puppeteer';
import { WebDriver, resolveLinuxChromiumExecutable, resolveBrowserCacheDirectory, validateLinuxBrowserRuntime } from '../src/web-driver.mjs';
import { mkdtempSync, rmSync, mkdirSync, writeFileSync, chmodSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

test('installed Chromium runs with its sandbox; actual WhatsApp driver is loadable', async context => {
  const directory = mkdtempSync(join(tmpdir(), 'lumar-wa-browser-test-'));
  context.after(() => rmSync(directory, { recursive: true }));
  const driver = new WebDriver(directory);
  assert.equal(driver.client.options.authStrategy.dataPath, join(directory, 'auth'));
  assert.equal(driver.client.options.authStrategy.clientId, 'lumar');
  assert.equal(driver.client.options.webVersionCache.path, join(directory, 'web-cache'));
  assert.equal(driver.client.options.puppeteer.headless, true);
  assert.equal(driver.client.options.puppeteer.args, undefined);
  assert.equal(driver.client.options.puppeteer.executablePath, undefined);
  assert.equal(JSON.stringify(driver.client.options.puppeteer).includes('--no-sandbox'), false);
  assert.equal(JSON.stringify(driver.client.options.puppeteer).includes('--disable-setuid-sandbox'), false);
  const browser = await puppeteer.launch({ headless: true, timeout: 30000, userDataDir: join(directory, 'probe') });
  try {
    const page = await browser.newPage();
    await page.setContent('<title>LUMAR local browser verification</title>');
    assert.equal(await page.title(), 'LUMAR local browser verification');
    driver.client.pupBrowser = browser;
    await driver.destroy();
    assert.equal(browser.connected, false);
    await driver.destroy();
  } finally { await browser.close(); }
});

test('Linux Chromium executable path rejects relative and missing files', { skip: process.platform !== 'linux' }, context => {
  const directory = mkdtempSync(join(tmpdir(), 'lumar-wa-chromium-test-'));
  context.after(() => rmSync(directory, { recursive: true, force: true }));

  process.env.LUMAR_LOCAL_CHROMIUM_EXECUTABLE_PATH = 'relative/chromium';
  assert.throws(() => resolveLinuxChromiumExecutable(), /Configured Chromium path is invalid/);

  process.env.LUMAR_LOCAL_CHROMIUM_EXECUTABLE_PATH = join(directory, 'missing-chromium');
  assert.throws(() => resolveLinuxChromiumExecutable(), /Configured Chromium path is invalid/);

  const executable = join(directory, 'chromium-test');
  writeFileSync(executable, '#!/bin/sh\nexit 0\n', { mode: 0o700 });
  chmodSync(executable, 0o700);
  process.env.LUMAR_LOCAL_CHROMIUM_EXECUTABLE_PATH = executable;
  assert.equal(resolveLinuxChromiumExecutable(), executable);

  delete process.env.LUMAR_LOCAL_CHROMIUM_EXECUTABLE_PATH;
  assert.equal(resolveLinuxChromiumExecutable(), undefined);
});

test('Linux browser cache is outside auth and survives cache deletion', { skip: process.platform !== 'linux' }, context => {
  const dataDirectory = mkdtempSync(join(tmpdir(), 'lumar-wa-data-test-'));
  const cacheDirectory = mkdtempSync(join(tmpdir(), 'lumar-wa-cache-test-'));
  context.after(() => rmSync(dataDirectory, { recursive: true, force: true }));
  context.after(() => rmSync(cacheDirectory, { recursive: true, force: true }));

  mkdirSync(join(dataDirectory, 'auth'), { recursive: true, mode: 0o700 });
  writeFileSync(join(dataDirectory, 'state.json'), '{"linked":true,"connect":true,"loggedOut":false}', { mode: 0o600 });
  process.env.LUMAR_LOCAL_BROWSER_CACHE_DIRECTORY = cacheDirectory;

  assert.equal(resolveBrowserCacheDirectory(dataDirectory), cacheDirectory);
  assert.equal(statSync(join(dataDirectory, 'auth')).isDirectory(), true);

  rmSync(cacheDirectory, { recursive: true, force: true });
  assert.equal(resolveBrowserCacheDirectory(dataDirectory), cacheDirectory);
  assert.equal(statSync(join(dataDirectory, 'auth')).isDirectory(), true);
  assert.equal(statSync(join(dataDirectory, 'state.json')).isFile(), true);

  delete process.env.LUMAR_LOCAL_BROWSER_CACHE_DIRECTORY;
});

test('Linux runtime rejects root and cache inside session data', { skip: process.platform !== 'linux' }, context => {
  const dataDirectory = mkdtempSync(join(tmpdir(), 'lumar-wa-root-test-'));
  context.after(() => rmSync(dataDirectory, { recursive: true, force: true }));

  if (typeof process.geteuid === 'function' && process.geteuid() === 0) {
    assert.throws(() => validateLinuxBrowserRuntime(), /Gateway must not run as root/);
  } else {
    validateLinuxBrowserRuntime();
  }

  process.env.LUMAR_LOCAL_BROWSER_CACHE_DIRECTORY = join(dataDirectory, 'web-cache');
  assert.throws(() => resolveBrowserCacheDirectory(dataDirectory), /Browser cache directory is unavailable/);
  delete process.env.LUMAR_LOCAL_BROWSER_CACHE_DIRECTORY;
});