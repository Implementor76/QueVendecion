let routes = [];

export function registerRoute(path, handler) {
  routes.push({ path, handler });
}

export function navigate(path) {
  window.history.pushState({}, '', path);
  render();
}

function matchRoute(pathname) {
  for (const route of routes) {
    const routeParts = route.path.split('/');
    const pathParts = pathname.split('/');

    if (routeParts.length !== pathParts.length) continue;

    const params = {};
    let match = true;

    for (let i = 0; i < routeParts.length; i++) {
      if (routeParts[i].startsWith(':')) {
        params[routeParts[i].slice(1)] = pathParts[i];
      } else if (routeParts[i] !== pathParts[i]) {
        match = false;
        break;
      }
    }

    if (match) return { handler: route.handler, params };
  }
  return null;
}

function render() {
  const app = document.getElementById('app');
  const pathname = window.location.pathname;

  const result = matchRoute(pathname);
  if (result) {
    app.innerHTML = '';
    result.handler(app, result.params);
  } else {
    app.innerHTML = '';
    routes[0]?.handler(app, {});
  }
}

window.addEventListener('popstate', render);

document.addEventListener('click', (e) => {
  const link = e.target.closest('a[data-link]');
  if (link) {
    e.preventDefault();
    navigate(link.getAttribute('href'));
  }
});

export function initRouter() {
  render();
}
