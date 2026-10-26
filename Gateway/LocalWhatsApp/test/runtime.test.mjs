import test from 'node:test';
import assert from 'node:assert/strict';
import puppeteer from 'puppeteer';
import { WebDriver } from '../src/web-driver.mjs';
import { mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

test('installed Chromium runs with its sandbox; actual WhatsApp driver is loadable', async context => {
  const directory = mkdtempSync(join(tmpdir(), 'lumar-wa-browser-test-'));
  context.after(() => rmSync(directory, { recursive: true }));
  const driver = new WebDriver(directory);
  assert.equal(driver.client.options.authStrategy.dataPath, join(directory, 'auth'));
  assert.equal(driver.client.options.puppeteer.args, undefined);
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