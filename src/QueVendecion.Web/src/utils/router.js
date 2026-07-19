let currentRoute = '';
let routes = {};

export function registerRoute(path, handler) {
  routes[path] = handler;
}

export function navigate(path) {
  window.history.pushState({}, '', path);
  render();
}

function render() {
  const app = document.getElementById('app');
  const path = window.location.pathname;

  const handler = routes[path] || routes['/'];
  if (handler) {
    app.innerHTML = '';
    handler(app);
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
