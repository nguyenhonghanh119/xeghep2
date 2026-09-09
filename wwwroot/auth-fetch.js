(() => {
  const originalFetch = window.fetch.bind(window);
  window.fetch = (input, init = {}) => {
    const method = (init.method || 'GET').toUpperCase();
    if (!['GET', 'HEAD', 'OPTIONS', 'TRACE'].includes(method)) {
      const token = document.cookie.split('; ').find(x => x.startsWith('XSRF-TOKEN='))?.split('=')[1];
      if (token) {
        const headers = new Headers(init.headers || {});
        headers.set('X-XSRF-TOKEN', decodeURIComponent(token));
        init = { ...init, headers, credentials: 'same-origin' };
      }
    }
    return originalFetch(input, init);
  };
})();

async function logoutDriver(event) {
  if (event) event.preventDefault();
  try {
    await fetch('/api/auth/logout', { method: 'POST' });
  } finally {
    window.location.href = '/Login';
  }
  return false;
}
