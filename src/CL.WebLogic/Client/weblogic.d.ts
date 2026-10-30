// Type declarations for the WebLogic client runtime (weblogic.js / weblogic.esm.js).
// Hand-maintained alongside Client/src; ships in the NuGet package under client/.

export interface WebLogicConfig {
  version: string;
  basePath: string;
  csrfHeader: string;
  csrfField: string;
  csrfUrl: string;
  navigation: boolean;
  debug: boolean;
  realtime: RealtimeSettings | null;
  /** One-shot message carried from a plain form post; shown as a toast at start, then cleared. */
  flash: { message: string; variant: 'success' | 'info' | 'warning' | 'danger' } | null;
}

export interface RealtimeSettings {
  transport?: 'sse' | 'ws';
  sseUrl?: string;
  wsUrl?: string;
  channels?: string[];
  autoConnect?: boolean;
}

export interface Problem {
  status: number;
  type: string;
  title: string;
  detail: string;
  errors: Record<string, string[]>;
}

export declare class HttpError extends Error {
  readonly status: number;
  readonly problem: Problem;
  readonly response: Response | null;
  /** The parsed error body as it came (JSON or text), for errors that are not ProblemDetails. */
  readonly data: unknown;
}

export interface RequestOptions {
  method?: string;
  headers?: HeadersInit;
  body?: BodyInit;
  json?: unknown;
  form?: FormData | URLSearchParams;
  /** Requests with the same key cancel each other (last one wins). */
  key?: string;
  /** Milliseconds; 0 disables. Default 30000. */
  timeout?: number;
  signal?: AbortSignal;
  /** Retry once with a fresh CSRF token after a 403. Default true. */
  retryCsrf?: boolean;
}

export interface HttpResult<T = unknown> {
  response: Response;
  data: T;
  status: number;
  headers: Headers;
}

export interface Http {
  request<T = unknown>(url: string, options?: RequestOptions): Promise<HttpResult<T>>;
  get<T = unknown>(url: string, options?: RequestOptions): Promise<HttpResult<T>>;
  post<T = unknown>(url: string, json?: unknown, options?: RequestOptions): Promise<HttpResult<T>>;
  csrfToken(refresh?: boolean): Promise<string>;
  setToken(token: string | null): void;
  abort(key: string): void;
}

export type Variant = 'success' | 'info' | 'warning' | 'danger' | 'error' | 'primary' | 'secondary';

/** A UI adapter; any method left out falls back to the plain adapter. */
export interface UiAdapter {
  name?: string;
  toast?(message: string, variant: Variant, options: { duration?: number }): Element;
  overlay?(title: string, message: string, variant: Variant, duration: number): Element;
  showErrors?(form: HTMLFormElement, errors: Record<string, string[]>, summary: string): void;
  fieldError?(form: HTMLFormElement, name: string, message: string | null): boolean;
  clearErrors?(form: HTMLFormElement): void;
  progressStart?(): void;
  progressDone?(failed: boolean): void;
  announce?(message: string): void;
}

export interface Ui {
  setAdapter(adapter: UiAdapter): void;
  readonly adapter: UiAdapter;
  toast(message: string, variant?: Variant, options?: { duration?: number }): Element;
  overlay(title: string, message: string, variant?: Variant, duration?: number): Element;
  showErrors(form: HTMLFormElement, errors: Record<string, string[]>, summary?: string): void;
  fieldError(form: HTMLFormElement, name: string, message: string | null): boolean;
  clearErrors(form: HTMLFormElement): void;
  progress: { start(): void; done(failed?: boolean): void };
  announce(message: string): void;
}

export type Command =
  | { type: 'toast'; message: string; variant?: Variant }
  | { type: 'overlay'; title: string; message: string; variant?: Variant; duration?: number }
  | { type: 'redirect' | 'navigate'; url: string }
  | { type: 'reload' }
  | { type: 'replace'; selector: string; html: string }
  | { type: 'remove'; selector: string }
  | { type: 'addClass' | 'removeClass'; selector: string; className: string };

export interface FormField {
  name: string;
  label?: string;
  type?: string;
  required?: boolean;
  minLength?: number;
  maxLength?: number;
  min?: number | string;
  max?: number | string;
  pattern?: string;
  equalTo?: string;
  equalToLabel?: string;
  visible?: boolean;
  /** Validated on the server when the field loses focus. */
  remote?: boolean;
  /** Shown only while another field has one of the values. */
  showWhen?: { field: string; values: string[] };
  messages?: Record<string, string>;
}

export interface DashboardItem {
  instanceId: string;
  widgetName: string;
  zone: string;
  order: number;
  parameters: Record<string, string>;
}

export interface FormResult {
  ok: boolean;
  formId?: string;
  errors?: Record<string, string[] | string>;
  summary?: string;
  message?: string;
  redirect?: string;
  commands?: Command[];
  data?: unknown;
  step?: string;
}

export interface RealtimeEnvelope<T = unknown> {
  id: string | null;
  type: string;
  channel: string | null;
  data: T;
  timestamp: string | null;
}

export interface Realtime {
  connect(options?: { transport?: 'sse' | 'ws'; url?: string; channels?: string[] }): void;
  disconnect(): void;
  send(message: unknown): boolean;
  subscribe<T = unknown>(type: string, handler: (envelope: RealtimeEnvelope<T>) => void): () => void;
  readonly status: 'closed' | 'connecting' | 'open' | 'reconnecting';
  readonly lastEventId: string | null;
}

export type ModuleFactory = (element: Element, webLogic: WebLogicApi) => void | (() => void) | { unmount(): void };

/** Passkeys (WebAuthn). Begin endpoints return { options, state }; finish endpoints receive { credential, state, ...extra }. */
export interface Passkeys {
  isSupported(): boolean;
  isAutofillSupported(): Promise<boolean>;
  /** navigator.credentials.create from creation options JSON; resolves with the credential's JSON form. */
  create(options: Record<string, unknown>, init?: { signal?: AbortSignal }): Promise<Record<string, unknown>>;
  /** navigator.credentials.get from request options JSON; resolves with the credential's JSON form. */
  get(options: Record<string, unknown>, init?: { signal?: AbortSignal; conditional?: boolean }): Promise<Record<string, unknown>>;
  register<T = unknown>(beginUrl: string, finishUrl: string, extra?: { name?: string }): Promise<T>;
  signIn<T = unknown>(beginUrl: string, finishUrl: string, extra?: { remember?: boolean }): Promise<T>;
  /** Second factor or step-up. */
  verify<T = unknown>(beginUrl: string, finishUrl: string, extra?: Record<string, unknown>): Promise<T>;
  /** Offers passkeys in the username field's autofill (autocomplete="username webauthn"); null when unsupported. */
  startAutofill<T = unknown>(beginUrl: string, finishUrl: string, extra?: Record<string, unknown>): Promise<T | null>;
  stopAutofill(): void;
  bind(root: Element): void;
}

export interface Bus {
  on(type: string, handler: (detail: any, event: CustomEvent) => void): () => void;
  once(type: string, handler: (detail: any) => void): () => void;
  emit(type: string, detail?: unknown): void;
}

export interface WebLogicApi {
  readonly version: string;
  readonly config: WebLogicConfig;
  readonly bus: Bus;
  on(type: string, handler: (detail: any, event: CustomEvent) => void): () => void;
  readonly http: Http;
  readonly ui: Ui;
  readonly dom: {
    el(tag: string, attributes?: Record<string, unknown> | null, ...children: unknown[]): HTMLElement;
    append(node: Node, children: unknown[]): Node;
    qs(root: ParentNode, selector: string): Element | null;
    qsa(root: ParentNode, selector: string): Element[];
    /** Server-rendered HTML only; executable scripts are removed. */
    parseHtml(html: string): DocumentFragment;
    swap(target: Element, html: string, mode?: 'inner' | 'outer' | 'append' | 'prepend' | 'before' | 'after'): Element[];
    readJson(root: ParentNode, selector: string): unknown;
    setBusy(element: HTMLElement, busy: boolean): void;
  };
  readonly modules: { define(name: string, factory: ModuleFactory): void; mount(root: Element): void; unmount(root: Element): void };
  readonly commands: { run(commands: Command[]): void };
  readonly forms: {
    bind(root: Element): void;
    submit(form: HTMLFormElement, submitter?: HTMLElement | null): Promise<FormResult | null>;
    validate(form: HTMLFormElement): Record<string, string[]>;
    readValues(form: HTMLFormElement): Record<string, FormDataEntryValue | FormDataEntryValue[]>;
    /** Re-applies the show-when conditions of a form. */
    refresh(form: HTMLFormElement): void;
    /** Adds an item to a repeater (element with data-wl-repeater); null at the maximum. */
    addItem(repeater: Element): Element | null;
  };
  readonly validation: {
    validateField(field: FormField, value: unknown, values?: Record<string, unknown>): string | null;
    validateAll(schema: { fields?: FormField[] }, values: Record<string, unknown>): Record<string, string[]>;
  };
  readonly widgets: { action(element: HTMLElement): Promise<unknown>; refresh(element: HTMLElement): Promise<boolean> };
  readonly dashboard: {
    setEditing(dashboard: Element, on: boolean): void;
    save(dashboard: Element, items?: DashboardItem[]): Promise<{ key: string; version: number; items: DashboardItem[] } | null>;
    reload(dashboard: Element): Promise<Element>;
    add(dashboard: Element, widgetName: string, zone?: string, parameters?: Record<string, string>): Promise<Element | null>;
    remove(item: Element): Promise<unknown>;
    move(item: Element, zone: Element, index: number): Promise<unknown>;
    readLayout(dashboard: Element): DashboardItem[];
  };
  readonly realtime: Realtime;
  readonly passkeys: Passkeys;
  navigate(url: string): Promise<boolean>;
  enhance(root: Element): void;
  readonly url: { isLocalUrl(url: unknown): boolean; isSameOrigin(url: string, origin: string): boolean };
  start(): void;
}

export declare const WebLogic: WebLogicApi | null;

declare global {
  interface Window {
    WebLogic: WebLogicApi;
    WebLogicPending?: Array<(webLogic: WebLogicApi) => void>;
  }
}
