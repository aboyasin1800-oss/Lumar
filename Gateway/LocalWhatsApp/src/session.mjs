export class GatewayError extends Error {
  constructor(code, status = 409) {
    super(code);
    this.code = code;
    this.status = status;
  }
}

export class SessionManager {
  constructor(driverFactory, store, { pairingLifetimeMs = 45000 } = {}) {
    this.driverFactory = driverFactory;
    this.store = store;
    this.pairingLifetimeMs = pairingLifetimeMs;
    this.state = 'NotLinked';
    this.currentNumber = null;
    this.lastError = null;
    this.pairing = null;
    this.driver = null;
    this.busy = false;
    this.saved = store.read();
    if (this.saved.linked) this.state = 'Disconnected';
    if (this.saved.loggedOut) this.state = 'LoggedOut';
  }

  status() {
    return { state: this.state, currentNumber: this.currentNumber,
      lastError: this.lastError, linked: this.saved.linked, operationalActivation: false };
  }

  linkMaterial() {
    if (!this.pairing || Date.now() >= this.pairing.expiresAt) return { kind: null, value: null, expiresAt: null };
    return { kind: 'qr', value: this.pairing.value, expiresAt: new Date(this.pairing.expiresAt).toISOString() };
  }

  persist(patch) {
    this.saved = { ...this.saved, ...patch };
    this.store.write(this.saved);
  }

  async exclusive(action) {
    if (this.busy) throw new GatewayError('BUSY');
    this.busy = true;
    try { return await action(); }
    finally { this.busy = false; }
  }

  async restore() {
    if (this.saved.linked && this.saved.connect) await this.command('reconnect');
  }

  async command(action) {
    return this.exclusive(async () => {
      if (action === 'disconnect') {
        this.persist({ connect: false });
        await this.closeDriver();
        this.state = this.saved.linked ? 'Disconnected' : 'NotLinked';
      } else if (action === 'logout' || action === 'replace') {
        if (this.saved.linked && this.state !== 'Connected') throw new GatewayError('RECONNECT_REQUIRED');
        if (this.saved.linked) {
          await this.driver.logout();
        }
        await this.closeDriver();
        this.persist({ linked: false, connect: false, loggedOut: true });
        this.state = 'LoggedOut';
        if (action === 'replace') await this.start(false);
      } else if (action === 'link') {
        if (this.saved.linked) throw new GatewayError('ALREADY_LINKED');
        if (this.driver) throw new GatewayError('BUSY');
        await this.start(false);
      } else if (action === 'reconnect') {
        if (!this.saved.linked) throw new GatewayError('NOT_LINKED');
        if (this.state === 'Connected') return this.status();
        await this.closeDriver();
        await this.start(true);
      } else {
        throw new GatewayError('INVALID_ACTION', 400);
      }
      return this.status();
    });
  }

  async start(recovery) {
    this.state = recovery ? 'Reconnecting' : 'Linking';
    this.lastError = null;
    this.currentNumber = null;
    this.persist({ connect: true, loggedOut: false });
    const driver = this.driverFactory();
    this.driver = driver;
    const safely = (action) => (...args) => {
      if (this.driver !== driver) return;
      try { action(...args); }
      catch { this.state = 'Error'; this.lastError = 'SESSION_STORAGE_ERROR'; this.pairing = null; }
    };
    driver.on('qr', safely(value => {
      this.state = 'Linking';
      this.currentNumber = null;
      this.pairing = { value, expiresAt: Date.now() + this.pairingLifetimeMs };
    }));
    driver.on('ready', safely(number => {
      if (!/^[1-9][0-9]{7,14}$/.test(number ?? '')) {
        this.state = 'Error';
        this.lastError = 'CURRENT_NUMBER_UNCONFIRMED';
        this.pairing = null;
        return;
      }
      this.persist({ linked: true, connect: true, loggedOut: false });
      this.state = 'Connected';
      this.currentNumber = number;
      this.pairing = null;
      this.lastError = null;
    }));
    driver.on('disconnected', safely(loggedOut => {
      this.state = loggedOut ? 'LoggedOut' : 'Disconnected';
      this.currentNumber = null;
      this.pairing = null;
      this.lastError = loggedOut ? 'SESSION_LOGGED_OUT' : 'CONNECTION_LOST';
      if (loggedOut) this.persist({ linked: false, connect: false, loggedOut: true });
    }));
    driver.on('failure', safely(() => {
      this.state = 'Error';
      this.currentNumber = null;
      this.pairing = null;
      this.lastError = 'SESSION_CONNECTION_FAILED';
    }));
    driver.initialize();
  }

  async closeDriver() {
    const driver = this.driver;
    this.driver = null;
    this.pairing = null;
    this.currentNumber = null;
    if (driver) await driver.destroy();
  }

  async stop() {
    await this.exclusive(() => this.closeDriver());
  }

  async send(message) {
    return this.exclusive(async () => {
      if (this.state !== 'Connected' || !this.driver) throw new GatewayError('NOT_CONNECTED', 503);
      try {
        const providerMessageId = await this.driver.send(message);
        if (!providerMessageId) throw new GatewayError('SEND_UNCONFIRMED', 502);
        return { outcome: 'Sent', providerMessageId, retryable: false };
      } catch (error) {
        const reason = error instanceof Error ? error.message : String(error);
        this.lastError = 'SEND_UNCONFIRMED';
        console.error(JSON.stringify({ stage: 'session-send-failed', error: reason }));
        throw new GatewayError('SEND_UNCONFIRMED', 502);
      }
    });
  }
}