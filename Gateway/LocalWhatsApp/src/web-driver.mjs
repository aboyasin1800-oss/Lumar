import { EventEmitter } from 'node:events';
import { join } from 'node:path';
import whatsapp from 'whatsapp-web.js';

const { Client, LocalAuth, MessageMedia } = whatsapp;

export class WebDriver extends EventEmitter {
  constructor(directory) {
    super();
    this.stopped = false;
    this.client = new Client({
      authStrategy: new LocalAuth({ clientId: 'lumar', dataPath: join(directory, 'auth') }),
      webVersionCache: { type: 'local', path: join(directory, 'web-cache') },
      puppeteer: { headless: true, timeout: 30000 },
      authTimeoutMs: 60000,
      qrMaxRetries: 4,
      deviceName: 'LUMAR Local Test Gateway',
      browserName: 'Chrome',
    });
    this.client.on('qr', value => { if (!this.stopped) this.emit('qr', value); });
    this.client.on('ready', () => {
      if (this.stopped) return;
      clearTimeout(this.timer);
      const identity = this.client.info?.wid;
      this.emit('ready', identity?.server === 'c.us' ? identity.user : null);
    });
    this.client.on('auth_failure', () => { if (!this.stopped) this.emit('failure'); });
    this.client.on('disconnected', reason => { if (!this.stopped) this.emit('disconnected', reason === 'LOGOUT'); });
  }

  initialize() {
    this.timer = setTimeout(() => {
      if (!this.stopped) this.emit('failure');
      void this.destroy().catch(() => this.emit('failure'));
    }, 180000);
    this.timer.unref();
    this.initializing = this.client.initialize().then(async () => {
      if (this.stopped) await this.compatibleClient().destroy();
    }).catch(() => { if (!this.stopped) this.emit('failure'); });
  }

  async destroy() {
    this.stopped = true;
    clearTimeout(this.timer);
    await this.compatibleClient().destroy();
    await this.initializing;
    await this.compatibleClient().destroy();
  }

  compatibleClient() {
    const browser = this.client.pupBrowser;
    if (browser && typeof browser.isConnected !== 'function') {
      browser.isConnected = () => browser.connected;
    }
    return this.client;
  }

  async logout() {
    await this.compatibleClient().logout();
  }

  async send({ recipient, text, media }) {
    const content = media ? new MessageMedia(media.mimeType, media.base64, 'lumar-image') : text;
    const message = await this.client.sendMessage(`${recipient}@c.us`, content,
      { caption: media ? text : undefined, sendSeen: false, waitUntilMsgSent: true });
    return message?.id?._serialized ?? null;
  }
}