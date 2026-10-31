import { EventEmitter } from 'node:events';
import { join } from 'node:path';
import whatsapp from 'whatsapp-web.js';

const { Client, LocalAuth, MessageMedia } = whatsapp;

export class WebDriver extends EventEmitter {
  constructor(directory) {
    super();
    this.stopped = false;
    this.confirmationTimeoutMs = 15000;
    this.pendingConfirmations = [];
    this.recentOfficialMessageCreates = [];
    this.knownProviderIds = new Map();
    this._boundClient = null;
    this.lastSendDiagnostics = {
      SEND_MESSAGE_RETURNED: 'NO',
      RETURN_OBJECT_TYPE: 'null',
      MESSAGE_ID_PRESENT: 'NO',
      MESSAGE_ID_SERIALIZED_PRESENT: 'NO',
      MESSAGE_ID_ID_PRESENT: 'NO',
      EXTRACTED_PROVIDER_ID_PRESENT: 'NO',
      SEND_EXCEPTION_TYPE: null,
    };
    this.lastAckDiagnostics = {
      ACK_RECEIVED: 'NO',
      ACK_LEVEL: null,
      TRUSTED_PROVIDER_ID_PRESENT: 'NO',
    };
    this.client = new Client({
      authStrategy: new LocalAuth({ clientId: 'lumar', dataPath: join(directory, 'auth') }),
      webVersionCache: { type: 'local', path: join(directory, 'web-cache') },
      puppeteer: { headless: true, timeout: 30000 },
      authTimeoutMs: 60000,
      qrMaxRetries: 4,
      deviceName: 'LUMAR Local Test Gateway',
      browserName: 'Chrome',
    });
    this.ensureClientBindings();
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

  ensureClientBindings() {
    if (!this.client || typeof this.client.on !== 'function') return;
    if (this._boundClient === this.client) return;
    this.client.on('message_create', message => this.handleOutgoingMessageCreate(message));
    this.client.on('message_ack', (message, ack) => this.handleOutgoingMessageAck(message, ack));
    this._boundClient = this.client;
  }

  trackKnownProviderId(providerMessageId) {
    if (!providerMessageId || !isSafeProviderValue(providerMessageId)) return;
    const current = this.knownProviderIds.get(providerMessageId) ?? { status: 'Sent', ackLevel: null, seenAt: Date.now() };
    this.knownProviderIds.set(providerMessageId, current);
    return current;
  }

  recordAck(providerMessageId, ackValue) {
    if (!providerMessageId || !isSafeProviderValue(providerMessageId)) return null;
    const current = this.knownProviderIds.get(providerMessageId);
    if (!current) {
      return null;
    }
    if (Number.isFinite(ackValue) && current.ackLevel === ackValue) {
      return current;
    }
    const normalizedAck = Number.isFinite(ackValue) ? ackValue : null;
    if (normalizedAck === null) return current;
    if (normalizedAck > (current.ackLevel ?? -1)) {
      current.ackLevel = normalizedAck;
      current.status = normalizedAck >= 3 ? 'Delivered' : normalizedAck >= 1 ? 'Sent' : current.status;
      current.seenAt = Date.now();
    }
    this.knownProviderIds.set(providerMessageId, current);
    return current;
  }

  handleOutgoingMessageCreate(message) {
    if (!message || typeof message !== 'object') return;
    if (message.fromMe !== true) {
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'message-create-filtered',
        outcome: 'rejected',
        reason: 'fromMe=false',
      }));
      return;
    }
    const providerMessageId = extractProviderMessageId(message);
    if (!providerMessageId) {
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'message-create-filtered',
        outcome: 'rejected',
        reason: 'missing-trusted-id',
      }));
      return;
    }

    const messageKey = {
      recipient: normalizeChatRecipient(message?.to?._serialized ?? message?.to ?? message?.from?._serialized ?? message?.from),
      text: String(message?.body ?? message?.caption ?? '').trim(),
      mediaSignature: null,
    };
    this.recentOfficialMessageCreates = this.recentOfficialMessageCreates.filter(item => Date.now() - item.createdAt < this.confirmationTimeoutMs);
    this.recentOfficialMessageCreates.push({
      ...messageKey,
      providerMessageId,
      createdAt: Date.now(),
    });

    this.trackKnownProviderId(providerMessageId);

    const request = this.pendingConfirmations.find(item => !item.resolved && item.eligible(message));
    if (!request) {
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'confirmation-correlation',
        outcome: 'replayed-early',
        reason: 'no-matching-pending-request-at-event-time',
        trustedProviderIdPresent: true,
      }));
      return;
    }

    request.resolved = true;
    clearTimeout(request.timer);
    this.pendingConfirmations = this.pendingConfirmations.filter(item => item !== request);
    this.recentOfficialMessageCreates = this.recentOfficialMessageCreates.filter(item => item.providerMessageId !== providerMessageId || item.recipient !== messageKey.recipient || item.text !== messageKey.text);
    request.resolve(providerMessageId);
    console.error(JSON.stringify({
      marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
      stage: 'confirmation-correlation',
      outcome: 'matched',
      reason: 'official-message-create',
      trustedProviderIdPresent: true,
    }));
  }

  handleOutgoingMessageAck(message, ack) {
    const providerMessageId = extractProviderMessageId(message);
    const ackValue = typeof ack === 'number' ? ack : Number(ack);
    this.lastAckDiagnostics = {
      ACK_RECEIVED: providerMessageId ? 'YES' : 'NO',
      ACK_LEVEL: Number.isFinite(ackValue) ? ackValue : null,
      TRUSTED_PROVIDER_ID_PRESENT: providerMessageId ? 'YES' : 'NO',
    };
    if (!providerMessageId || !isSafeProviderValue(providerMessageId)) {
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'message-ack',
        outcome: 'ignored',
        reason: 'untrusted-or-missing-provider-id',
        ackLevel: Number.isFinite(ackValue) ? ackValue : null,
      }));
      return;
    }
    const known = this.recordAck(providerMessageId, ackValue);
    if (!known) {
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'message-ack',
        outcome: 'ignored',
        reason: 'no-known-provider-id',
        ackLevel: Number.isFinite(ackValue) ? ackValue : null,
      }));
      return;
    }
    console.error(JSON.stringify({
      marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
      stage: 'message-ack',
      outcome: known.status === 'Delivered' ? 'delivered' : 'accepted',
      ackLevel: known.ackLevel,
      trustedProviderIdPresent: true,
      duplicate: Number.isFinite(ackValue) && known.ackLevel === ackValue && known.seenAt !== Date.now(),
    }));
  }

  async send({ recipient, text, media }) {
    this.ensureClientBindings();
    const chatId = `${recipient}@c.us`;
    const mediaFileName = media?.filename || 'lumar-image';
    const mediaPayload = media ? new MessageMedia(media.mimeType, media.base64, mediaFileName) : null;
    const sendOptions = media ? { caption: text, sendSeen: false, waitUntilMsgSent: true } : undefined;
    const confirmationWait = this.waitForOfficialSendConfirmation({ recipient, text, media });

    this.lastSendDiagnostics = {
      SEND_MESSAGE_RETURNED: 'NO',
      RETURN_OBJECT_TYPE: 'null',
      MESSAGE_ID_PRESENT: 'NO',
      MESSAGE_ID_SERIALIZED_PRESENT: 'NO',
      MESSAGE_ID_ID_PRESENT: 'NO',
      EXTRACTED_PROVIDER_ID_PRESENT: 'NO',
      SEND_EXCEPTION_TYPE: null,
    };

    console.error(JSON.stringify({
      marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
      stage: 'whatsapp-media-send',
      hasMedia: Boolean(media),
      mimeType: media?.mimeType ?? null,
      dataBytes: media ? Buffer.byteLength(media.base64, 'base64') : 0,
      filename: mediaFileName,
      callShape: media ? 'client.sendMessage(chatId, media, { caption: text, sendSeen: false, waitUntilMsgSent: true })' : 'client.sendMessage(chatId, text)',
    }));

    try {
      const content = media ? mediaPayload : text;
      const message = await this.client.sendMessage(chatId, content, sendOptions);
      this.lastSendDiagnostics.SEND_MESSAGE_RETURNED = message === undefined ? 'NO' : 'YES';
      this.lastSendDiagnostics.RETURN_OBJECT_TYPE = message === null ? 'null' : message === undefined ? 'undefined' : Array.isArray(message) ? 'array' : typeof message;

      const providerMessageId = extractProviderMessageId(message);
      this.lastSendDiagnostics.MESSAGE_ID_PRESENT = message && typeof message === 'object' && 'id' in message ? 'YES' : 'NO';
      this.lastSendDiagnostics.MESSAGE_ID_SERIALIZED_PRESENT = typeof message?.id?._serialized === 'string' && isSafeProviderValue(message.id._serialized) ? 'YES' : 'NO';
      this.lastSendDiagnostics.MESSAGE_ID_ID_PRESENT = typeof message?.id?.id === 'string' && isSafeProviderValue(message.id.id) ? 'YES' : 'NO';
      this.lastSendDiagnostics.EXTRACTED_PROVIDER_ID_PRESENT = providerMessageId ? 'YES' : 'NO';
      this.lastSendDiagnostics.SEND_EXCEPTION_TYPE = null;
      if (providerMessageId) {
        this.trackKnownProviderId(providerMessageId);
        const request = this.pendingConfirmations.find(item => !item.resolved && item.recipient === normalizeChatRecipient(recipient) && item.text === String(text ?? '').trim() && item.mediaSignature === (media ? `${media.mimeType}:${media.filename}:${Buffer.byteLength(media.base64, 'base64')}` : null));
        if (request) {
          request.resolved = true;
          clearTimeout(request.timer);
          this.pendingConfirmations = this.pendingConfirmations.filter(item => item !== request);
          request.resolve(providerMessageId);
        }
      }
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'whatsapp-send-result',
        outcome: providerMessageId ? 'accepted' : 'unconfirmed',
        providerMessageIdPresent: Boolean(providerMessageId),
        hasMedia: Boolean(media),
      }));
      if (providerMessageId) return providerMessageId;

      const confirmedId = await confirmationWait;
      if (confirmedId) this.trackKnownProviderId(confirmedId);
      this.lastSendDiagnostics.EXTRACTED_PROVIDER_ID_PRESENT = confirmedId ? 'YES' : 'NO';
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'official-confirmation-result',
        outcome: confirmedId ? 'sent' : 'unconfirmed',
        trustedProviderIdPresent: Boolean(confirmedId),
      }));
      return confirmedId;
    } catch (error) {
      this.lastSendDiagnostics.SEND_MESSAGE_RETURNED = 'NO';
      this.lastSendDiagnostics.RETURN_OBJECT_TYPE = 'null';
      this.lastSendDiagnostics.MESSAGE_ID_PRESENT = 'NO';
      this.lastSendDiagnostics.MESSAGE_ID_SERIALIZED_PRESENT = 'NO';
      this.lastSendDiagnostics.MESSAGE_ID_ID_PRESENT = 'NO';
      this.lastSendDiagnostics.EXTRACTED_PROVIDER_ID_PRESENT = 'NO';
      this.lastSendDiagnostics.SEND_EXCEPTION_TYPE = error && typeof error === 'object' && 'name' in error && typeof error.name === 'string' ? error.name : (error instanceof Error ? 'Error' : String(error));
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'whatsapp-media-error',
        hasMedia: Boolean(media),
        outcome: 'error',
        exceptionType: this.lastSendDiagnostics.SEND_EXCEPTION_TYPE,
      }));
      throw error;
    }
  }

  waitForOfficialSendConfirmation({ recipient, text, media }) {
    return new Promise(resolve => {
      const request = {
        recipient: normalizeChatRecipient(recipient),
        text: typeof text === 'string' ? text.trim() : '',
        mediaSignature: media ? `${media.mimeType}:${media.filename}:${Buffer.byteLength(media.base64, 'base64')}` : null,
        resolved: false,
        eligible: (message) => {
          const eventChat = normalizeChatRecipient(message?.to?._serialized ?? message?.to ?? message?.from?._serialized ?? message?.from);
          const eventText = String(message?.body ?? message?.caption ?? '').trim();
          if (eventChat !== request.recipient || eventText !== request.text || message?.fromMe !== true) return false;
          if (media && !message?.caption) return false;
          return true;
        },
        resolve,
        timer: setTimeout(() => {
          this.pendingConfirmations = this.pendingConfirmations.filter(item => item !== request);
          this.recentOfficialMessageCreates = this.recentOfficialMessageCreates.filter(item => item.recipient !== request.recipient || item.text !== request.text || item.mediaSignature !== request.mediaSignature);
          if (!request.resolved) resolve(null);
        }, this.confirmationTimeoutMs),
      };

      const duplicate = this.pendingConfirmations.some(item => !item.resolved
        && item.recipient === request.recipient
        && item.text === request.text
        && item.mediaSignature === request.mediaSignature);
      if (duplicate) {
        console.error(JSON.stringify({
          marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
          stage: 'confirmation-correlation',
          outcome: 'rejected',
          reason: 'ambiguous-concurrent-send',
        }));
        resolve(null);
        return;
      }

      const replay = this.recentOfficialMessageCreates.find(item =>
        item.recipient === request.recipient
        && item.text === request.text
        && item.mediaSignature === request.mediaSignature
        && Date.now() - item.createdAt < this.confirmationTimeoutMs
      );
      if (replay) {
        this.recentOfficialMessageCreates = this.recentOfficialMessageCreates.filter(item => item !== replay);
        request.resolved = true;
        clearTimeout(request.timer);
        this.pendingConfirmations = this.pendingConfirmations.filter(item => item !== request);
        resolve(replay.providerMessageId);
        console.error(JSON.stringify({
          marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
          stage: 'confirmation-correlation',
          outcome: 'replayed-early',
          reason: 'recent-official-message-create-match',
          trustedProviderIdPresent: true,
        }));
        return;
      }

      this.pendingConfirmations.push(request);
      console.error(JSON.stringify({
        marker: 'LOCAL_WA_SAFE_OBSERVABILITY',
        stage: 'confirmation-wait',
        outcome: 'pending',
        timeoutMs: this.confirmationTimeoutMs,
      }));
    });
  }
}

function normalizeChatRecipient(value) {
  if (typeof value !== 'string') return null;
  const digits = value.replace(/\D+/g, '');
  if (!digits) return null;
  const normalized = digits.startsWith('00') ? digits.slice(2) : digits;
  if (normalized.startsWith('967')) {
    const local = normalized.slice(3);
    if (local.length === 9 && local.startsWith('7')) return `${normalized}@c.us`;
    return null;
  }
  if (normalized.length === 9 && normalized.startsWith('7')) return `967${normalized}@c.us`;
  return null;
}

function isSafeProviderValue(value) {
  if (typeof value !== 'string') return false;
  const normalized = value.trim();
  if (!normalized || normalized.length > 200) return false;
  if (normalized.startsWith('undefined') || normalized.startsWith('null')) return false;
  const lower = normalized.toLowerCase();
  const blocked = [
    'undefined', 'null', 'placeholder', 'fake', 'mock', 'test', 'example', 'invalid',
    'n/a', 'na', 'temp', 'unknown', 'fallback-id', 'sample-id', 'placeholder-id',
  ];
  if (blocked.some(token => lower === token || lower.includes(token))) return false;
  if (lower.startsWith('tok_')) return false;
  return true;
}

function extractProviderMessageId(message) {
  const official = [
    message?.id?._serialized,
    message?._serialized,
    message?.id?.['$1'],
  ];

  for (const value of official) {
    if (typeof value !== 'string') continue;
    const normalized = value.trim();
    if (isSafeProviderValue(normalized)) {
      return normalized;
    }
  }

  const nested = message?.id;
  if (nested && typeof nested === 'object') {
    const fromMe = typeof nested.fromMe === 'boolean' ? nested.fromMe : null;
    const remoteValue = nested.remote;
    const remote = typeof remoteValue === 'string' ? remoteValue.trim() : (
      remoteValue && typeof remoteValue === 'object' && typeof remoteValue._serialized === 'string' ? remoteValue._serialized.trim() : ''
    );
    const id = typeof nested.id === 'string' ? nested.id.trim() : '';

    if (fromMe !== null && remote.length > 0 && id.length > 0 && isSafeProviderValue(remote) && isSafeProviderValue(id)) {
      const reconstructed = `${fromMe}_${remote}_${id}`;
      if (reconstructed.length > 0 && reconstructed.length <= 200 && !reconstructed.startsWith('undefined')) {
        return reconstructed;
      }
    }
  }

  const direct = [
    typeof message?.id === 'string' ? message.id : null,
    message?.id?.id,
  ];

  for (const value of direct) {
    if (typeof value !== 'string') continue;
    const normalized = value.trim();
    if (isSafeProviderValue(normalized)) {
      return normalized;
    }
  }

  return null;
}