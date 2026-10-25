/**
 * Client-side field validation from the server's form schema
 * ({ fields: [{ name, label, type, required, minLength, maxLength, min, max, pattern, equalTo, messages }] }).
 * The server validates again; this is for fast feedback only.
 */

/** Default English messages ({label} and rule values are substituted). */
export const defaultMessages = Object.freeze({
  required: '{label} is required.',
  minLength: '{label} must be at least {minLength} characters.',
  maxLength: '{label} must be at most {maxLength} characters.',
  min: '{label} must be at least {min}.',
  max: '{label} must be at most {max}.',
  number: '{label} must be a number.',
  pattern: '{label} is not in the expected format.',
  email: '{label} must be a valid e-mail address.',
  url: '{label} must be a valid URL.',
  equalTo: '{label} must match {otherLabel}.'
});

const EMAIL = /^[^\s@<>",;]+@[^\s@<>",;.]+(\.[^\s@<>",;.]+)+$/;
const NUMERIC_TYPES = new Set(['number', 'range']);
const ORDERED_TEXT_TYPES = new Set(['date', 'time', 'datetime-local', 'month', 'week']);
const MAX_PATTERN_INPUT = 10000;

/**
 * Replaces {name} placeholders.
 * @param {string} template
 * @param {Record<string, unknown>} values
 */
export function format(template, values) {
  return String(template).replace(/\{(\w+)\}/g, (match, key) => (values[key] !== undefined && values[key] !== null ? String(values[key]) : match));
}

/** @param {unknown} value */
export function isEmpty(value) {
  return value === undefined || value === null || value === false
    || (typeof value === 'string' && value.trim() === '')
    || (Array.isArray(value) && value.length === 0)
    || (typeof File !== 'undefined' && value instanceof File && value.size === 0 && value.name === '');
}

/**
 * Validates one value; returns the error message or null.
 * @param {Record<string, any>} field
 * @param {unknown} value
 * @param {Record<string, unknown>} [values] All form values (for equalTo).
 * @param {Record<string, string>} [messages]
 */
export function validateField(field, value, values = {}, messages = defaultMessages) {
  const label = field.label || field.name;
  const message = (rule, extra = {}) =>
    format((field.messages && field.messages[rule]) || messages[rule] || defaultMessages[rule], { ...field, label, ...extra });

  if (isEmpty(value)) return field.required ? message('required') : null;

  if (!Array.isArray(value) && (typeof value === 'string' || typeof value === 'number')) {
    const text = String(value);
    const length = [...text].length;
    if (field.minLength != null && length < field.minLength) return message('minLength');
    if (field.maxLength != null && length > field.maxLength) return message('maxLength');
    if ((field.type === 'email' || field.email) && !EMAIL.test(text)) return message('email');
    if ((field.type === 'url' || field.url) && !isHttpUrl(text)) return message('url');

    if (NUMERIC_TYPES.has(field.type)) {
      const number = Number(text);
      if (text.trim() === '' || Number.isNaN(number)) return message('number');
      if (field.min != null && number < Number(field.min)) return message('min');
      if (field.max != null && number > Number(field.max)) return message('max');
    } else if (ORDERED_TEXT_TYPES.has(field.type)) {
      if (field.min != null && text < String(field.min)) return message('min');
      if (field.max != null && text > String(field.max)) return message('max');
    }

    if (field.pattern && text.length <= MAX_PATTERN_INPUT) {
      const regex = compile(field.pattern);
      if (regex && !regex.test(text)) return message('pattern');
    }
  }

  if (field.equalTo && values[field.equalTo] !== value) {
    return message('equalTo', { otherLabel: field.equalToLabel || field.equalTo });
  }
  return null;
}

/**
 * Validates every visible field; returns { field: [message] } for the failures.
 * @param {{ fields?: Record<string, any>[] }} schema
 * @param {Record<string, unknown>} values
 * @param {Record<string, string>} [messages]
 */
export function validateAll(schema, values, messages = defaultMessages) {
  const errors = {};
  for (const field of (schema && schema.fields) || []) {
    if (!field || !field.name || field.visible === false) continue;
    const error = validateField(field, values[field.name], values, messages);
    if (error) errors[field.name] = [error];
  }
  return errors;
}

/** @param {string} text */
function isHttpUrl(text) {
  try {
    const url = new URL(text);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

const compiled = new Map();

/** @param {string} pattern */
function compile(pattern) {
  if (!compiled.has(pattern)) {
    let regex = null;
    try {
      regex = new RegExp(`^(?:${pattern})$`, 'u');
    } catch {
      regex = null; // the server is authoritative
    }
    compiled.set(pattern, regex);
  }
  return compiled.get(pattern);
}
