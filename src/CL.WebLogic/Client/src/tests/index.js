/** Entry of weblogic.tests.js: runs every browser suite once the page is ready. */
import './dom.tests.js';
import './runtime.tests.js';
import './forms.tests.js';
import './formextras.tests.js';
import './dashboard.tests.js';
import { run } from './testkit.js';

export const started = true;

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => run(), { once: true });
else setTimeout(() => run(), 0);
