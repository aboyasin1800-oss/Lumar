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
    const chatId = `${recipient}@c.us`;
    const mediaFileName = media?.filename || 'lumar-image';
    const mediaPayload = media ? new MessageMedia(media.mimeType, media.base64, mediaFileName) : null;
    const sendOptions = media ? { caption: text, sendSeen: false, waitUntilMsgSent: true } : undefined;

    console.error(JSON.stringify({
      stage: 'whatsapp-media-send',
      hasMedia: Boolean(media),
      chatId,
      mimeType: media?.mimeType ?? null,
      dataBytes: media ? Buffer.byteLength(media.base64, 'base64') : 0,
      filename: mediaFileName,
      callShape: media ? 'client.sendMessage(chatId, media, { caption: text, sendSeen: false, waitUntilMsgSent: true })' : 'client.sendMessage(chatId, text)',
    }));

    try {
      const content = media ? mediaPayload : text;
      const message = await this.client.sendMessage(chatId, content, sendOptions);
      const providerMessageId = extractProviderMessageId(message);
      console.error(JSON.stringify({
        stage: 'whatsapp-media-result',
        hasMedia: Boolean(media),
        providerMessageId: providerMessageId ?? null,
        messageType: message && typeof message === 'object' ? Object.keys(message).slice(0, 10) : null,
      }));
      return providerMessageId;
    } catch (error) {
      console.error(JSON.stringify({
        stage: 'whatsapp-media-error',
        hasMedia: Boolean(media),
        hasMessage: error instanceof Error ? error.message : String(error),
      }));
      throw error;
    }
  }
}

function extractProviderMessageId(message) {
  const nested = message?.id;
  if (nested && typeof nested === 'object') {
    const fromMe = typeof nested.fromMe === 'boolean' ? nested.fromMe : null;
    const remoteValue = nested.remote;
    const remote = typeof remoteValue === 'string' ? remoteValue.trim() : (
      remoteValue && typeof remoteValue === 'object' && typeof remoteValue._serialized === 'string' ? remoteValue._serialized.trim() : ''
    );
    const id = typeof nested.id === 'string' ? nested.id.trim() : '';
    if (fromMe !== null && remote.length > 0 && id.length > 0) {
      const reconstructed = `${fromMe}_${remote}_${id}`;
      if (reconstructed.length > 0 && reconstructed.length <= 200 && !reconstructed.startsWith('undefined')) {
        return reconstructed;
      }
    }
  }

  const direct = [
    message?.id?._serialized,
    message?._serialized,
    message?.id?.['$1'],
    message?.id?.id,
    typeof message?.id === 'string' ? message.id : null,
  ];

  for (const value of direct) {
    if (typeof value !== 'string') continue;
    const normalized = value.trim();
    if (normalized.length > 0 && normalized.length <= 200 && !normalized.startsWith('undefined')) {
      return normalized;
    }
  }

  return null;
}