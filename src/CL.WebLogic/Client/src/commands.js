/**
 * Server commands. Only the allowlisted command types run; URLs must be local, class names
 * well-formed. Unknown or malformed commands are ignored with a console warning.
 */
import { isLocalUrl } from './url.js';

/** Command types the client executes. */
export const COMMANDS = Object.freeze(['toast', 'overlay', 'redirect', 'navigate', 'reload', 'replace', 'remove', 'addClass', 'removeClass']);

const VARIANTS = new Set(['success', 'info', 'warning', 'danger', 'error', 'primary', 'secondary', 'light', 'dark']);
const CLASS_LIST = /^-?[_a-zA-Z][\w-]*( -?[_a-zA-Z][\w-]*)*$/;
const text = (value) => (typeof value === 'string' ? value : '');
const variant = (value) => (VARIANTS.has(value) ? value : 'info');

/**
 * Validates a command; returns a clean copy or null.
 * @param {unknown} command
 */
export function normalizeCommand(command) {
  if (!command || typeof command !== 'object') return null;
  const c = /** @type {Record<string, unknown>} */ (command);
  switch (c.type) {
    case 'toast':
      return text(c.message) ? { type: 'toast', message: text(c.message), variant: variant(c.variant) } : null;
    case 'overlay': {
      const duration = Number.isFinite(c.duration) ? Math.min(60000, Math.max(0, Number(c.duration))) : 2000;
      return { type: 'overlay', title: text(c.title), message: text(c.message), variant: variant(c.variant), duration };
    }
    case 'redirect':
    case 'navigate':
      return isLocalUrl(c.url) ? { type: c.type, url: /** @type {string} */ (c.url) } : null;
    case 'reload':
      return { type: 'reload' };
    case 'replace':
      return text(c.selector) && typeof c.html === 'string' ? { type: 'replace', selector: text(c.selector), html: c.html } : null;
    case 'remove':
      return text(c.selector) ? { type: 'remove', selector: text(c.selector) } : null;
    case 'addClass':
    case 'removeClass':
      return text(c.selector) && CLASS_LIST.test(text(c.className))
        ? { type: c.type, selector: text(c.selector), className: text(c.className) }
        : null;
    default:
      return null;
  }
}

/**
 * Creates the command runner.
 * @param {{ ui: any, navigate: (url: string) => any, swap: Function, enhance: (root: Element) => void,
 *           unmount: (root: Element) => void, location?: Location, root?: Document }} services
 */
export function createCommands({ ui, navigate, swap, enhance, unmount, location, root }) {
  const doc = root || globalThis.document;
  const loc = location || globalThis.location;

  const select = (selector) => {
    try {
      return [...doc.querySelectorAll(selector)];
    } catch {
      console.warn(`WebLogic: invalid selector '${selector}' in command`);
      return [];
    }
  };

  /** Runs a list of commands in order. */
  function run(commands) {
    for (const raw of Array.isArray(commands) ? commands : []) {
      const command = normalizeCommand(raw);
      if (!command) {
        console.warn('WebLogic: ignored command', raw);
        continue;
      }
      execute(command);
    }
  }

  function execute(command) {
    switch (command.type) {
      case 'toast': ui.toast(command.message, command.variant); break;
      case 'overlay': ui.overlay(command.title, command.message, command.variant, command.duration); break;
      case 'redirect': loc.assign(command.url); break;
      case 'navigate': navigate(command.url); break;
      case 'reload': loc.reload(); break;
      case 'replace':
        for (const target of select(command.selector)) {
          unmount(target);
          for (const inserted of swap(target, command.html, 'outer')) enhance(inserted);
        }
        break;
      case 'remove':
        for (const target of select(command.selector)) {
          unmount(target);
          target.remove();
        }
        break;
      case 'addClass':
      case 'removeClass':
        for (const target of select(command.selector)) {
          target.classList[command.type === 'addClass' ? 'add' : 'remove'](...command.className.split(' '));
        }
        break;
    }
  }

  return { run };
}
