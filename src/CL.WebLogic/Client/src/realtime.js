/**
 * Realtime client over server-sent events or WebSockets: reconnects with exponential back-off
 * and jitter, resumes from the last event id, and publishes envelopes
 * ({ id, type, channel, data }) on the bus as "realtime:message" and "realtime:<type>".
 */

/**
 * Delay before reconnect attempt n (0-based).
 * @param {number} attempt
 * @param {{ base?: number, max?: number, jitter?: number, random?: () => number }} [options]
 */
export function backoffDelay(attempt, { base = 1000, max = 30000, jitter = 0.25, random = Math.random } = {}) {
  const exponential = Math.min(max, base * 2 ** Math.max(0, attempt));
  const spread = exponential * jitter;
  return Math.max(0, Math.round(exponential - spread + random() * spread * 2));
}

const TYPE = /^[\w.:-]{1,100}$/;

/**
 * Parses and validates an envelope; null when malformed.
 * @param {unknown} raw JSON text or an object
 */
export function parseEnvelope(raw) {
  let value;
  try {
    value = typeof raw === 'string' ? JSON.parse(raw) : raw;
  } catch {
    return null;
  }
  if (!value || typeof value !== 'object' || typeof value.type !== 'string' || !TYPE.test(value.type)) return null;
  return {
    id: value.id === undefined || value.id === null ? null : String(value.id),
    type: value.type,
    channel: typeof value.channel === 'string' ? value.channel : null,
    data: value.data === undefined ? null : value.data,
    timestamp: typeof value.timestamp === 'string' ? value.timestamp : null
  };
}

/**
 * Converts a URL to ws:/wss:.
 * @param {string} url
 * @param {string} base
 */
export function toWebSocketUrl(url, base) {
  const parsed = new URL(url, base);
  parsed.protocol = parsed.protocol === 'https:' ? 'wss:' : 'ws:';
  return parsed.href;
}

/**
 * @param {{ bus: any, config: any, location?: Location }} services
 */
export function createRealtime({ bus, config, location }) {
  const loc = location || globalThis.location;
  let source = null;
  let socket = null;
  let timer = null;
  let attempt = 0;
  let lastEventId = null;
  let status = 'closed';
  let options = null;
  let stopped = true;

  const setStatus = (next) => {
    if (status === next) return;
    status = next;
    bus.emit('realtime:status', { status });
  };

  /**
   * Connects (replacing any existing connection).
   * @param {{ transport?: 'sse' | 'ws', url?: string, channels?: string[] }} [connectOptions]
   */
  function connect(connectOptions = {}) {
    disconnect();
    const settings = config.realtime || {};
    const transport = connectOptions.transport || settings.transport || 'sse';
    options = {
      transport,
      url: connectOptions.url || (transport === 'ws' ? settings.wsUrl || '/_weblogic/realtime/ws' : settings.sseUrl || '/_weblogic/realtime/sse'),
      channels: connectOptions.channels || settings.channels || []
    };
    stopped = false;
    attempt = 0;
    open();
  }

  function address() {
    const path = options.url.startsWith('/') ? (config.basePath || '') + options.url : options.url;
    const url = new URL(path, loc.href);
    if (lastEventId) url.searchParams.set('lastEventId', lastEventId);
    for (const channel of options.channels) url.searchParams.append('channel', channel);
    return url.href;
  }

  function open() {
    setStatus(attempt > 0 ? 'reconnecting' : 'connecting');
    if (options.transport === 'ws') {
      socket = new WebSocket(toWebSocketUrl(address(), loc.href));
      socket.addEventListener('open', opened);
      socket.addEventListener('message', (event) => receive(event.data, null));
      socket.addEventListener('close', () => {
        socket = null;
        retry();
      });
    } else {
      source = new EventSource(address());
      source.addEventListener('open', opened);
      source.addEventListener('message', (event) => receive(event.data, event.lastEventId));
      source.addEventListener('error', () => {
        source?.close();
        source = null;
        retry();
      });
    }
  }

  function opened() {
    attempt = 0;
    setStatus('open');
  }

  function receive(data, eventId) {
    const envelope = parseEnvelope(data);
    if (!envelope) return;
    if (eventId) lastEventId = eventId;
    else if (envelope.id) lastEventId = envelope.id;
    bus.emit('realtime:message', envelope);
    bus.emit('realtime:' + envelope.type, envelope);
  }

  function retry() {
    if (stopped) return;
    setStatus('reconnecting');
    clearTimeout(timer);
    timer = setTimeout(open, backoffDelay(attempt++));
  }

  /** Closes the connection and stops reconnecting. */
  function disconnect() {
    stopped = true;
    clearTimeout(timer);
    source?.close();
    socket?.close();
    source = null;
    socket = null;
    setStatus('closed');
  }

  /**
   * Sends a message (WebSocket transport only); false when not connected.
   * @param {unknown} message
   */
  function send(message) {
    if (!socket || socket.readyState !== 1) return false;
    socket.send(JSON.stringify(message));
    return true;
  }

  if (globalThis.addEventListener) {
    globalThis.addEventListener('online', () => {
      if (!stopped && status === 'reconnecting') {
        clearTimeout(timer);
        open();
      }
    });
  }

  return {
    connect,
    disconnect,
    send,
    /** @param {string} type @param {(envelope: any) => void} handler */
    subscribe: (type, handler) => bus.on('realtime:' + type, handler),
    get status() {
      return status;
    },
    get lastEventId() {
      return lastEventId;
    }
  };
}
