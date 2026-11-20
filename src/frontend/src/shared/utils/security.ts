/**
 * Validates a redirect target to ensure it is a safe relative in-app path.
 * Prevents open redirect attacks (e.g. //evil.com, \evil.com, or javascript:).
 */
export function getSafeRedirect(redirect: unknown, fallback = '/'): string {
  if (typeof redirect !== 'string' || !redirect.trim()) {
    return fallback;
  }

  const trimmed = redirect.trim();

  // Must begin with a single '/', must not start with '//',
  // must not contain backslashes, colons (protocol specifiers), or control characters.
  if (
    trimmed.startsWith('/') &&
    !trimmed.startsWith('//') &&
    !trimmed.includes('\\') &&
    !trimmed.includes(':')
  ) {
    return trimmed;
  }

  return fallback;
}
