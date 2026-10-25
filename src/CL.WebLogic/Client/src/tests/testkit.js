/**
 * A tiny browser test runner for the /_weblogic/tests page. Results are rendered into
 * #wl-test-results and published on globalThis.__wlTestResults for headless CI.
 */
import { el } from '../dom.js';

const tests = [];

/**
 * Registers a test. The function receives a fresh fixture element that is removed afterwards.
 * @param {string} name
 * @param {(fixture: HTMLElement) => any} fn
 */
export function test(name, fn) {
  tests.push({ name, fn });
}

const describe = (value) => {
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
};

export const assert = {
  ok(value, message = 'expected a truthy value') {
    if (!value) throw new Error(message);
  },
  equal(actual, expected, message) {
    if (!Object.is(actual, expected)) throw new Error(message || `expected ${describe(expected)}, got ${describe(actual)}`);
  },
  deepEqual(actual, expected, message) {
    if (describe(actual) !== describe(expected)) throw new Error(message || `expected ${describe(expected)}, got ${describe(actual)}`);
  },
  throws(fn, message = 'expected an exception') {
    try {
      fn();
    } catch {
      return;
    }
    throw new Error(message);
  }
};

/** Waits for the next animation frame plus a tick. */
export const tick = (ms = 0) => new Promise((resolve) => setTimeout(resolve, ms));

/** Runs all tests and renders the results. */
export async function run() {
  const output = document.getElementById('wl-test-results') || document.body;
  const failures = [];
  let passed = 0;
  for (const { name, fn } of tests) {
    const fixture = el('div', { id: 'wl-fixture' });
    document.body.append(fixture);
    try {
      await fn(fixture);
      passed++;
      output.append(el('li', { class: 'pass' }, '✔ ' + name));
    } catch (error) {
      failures.push({ name, message: String(error && error.message ? error.message : error) });
      output.append(el('li', { class: 'fail' }, '✘ ' + name + ' — ' + (error && error.message ? error.message : error)));
    } finally {
      fixture.remove();
    }
  }
  const summary = { done: true, passed, failed: failures.length, failures };
  globalThis.__wlTestResults = summary;
  document.title = (failures.length ? 'FAIL' : 'PASS') + ` ${passed}/${tests.length}`;
  output.prepend(el('p', { id: 'wl-test-summary', class: failures.length ? 'fail' : 'pass' }, document.title));
  return summary;
}
