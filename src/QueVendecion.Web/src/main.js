import './style.css';
import { registerRoute, initRouter } from './utils/router.js';
import { renderHome } from './pages/home.js';
import { renderTienda } from './pages/tienda.js';
import { renderPedidos } from './pages/pedidos.js';
import { renderMenu } from './pages/menu.js';
import { renderLogin } from './pages/login.js';
import { renderTiendaCliente } from './pages/tiendaCliente.js';

registerRoute('/', renderHome);
registerRoute('/login', renderLogin);
registerRoute('/mi-tienda', renderTienda);
registerRoute('/pedidos', renderPedidos);
registerRoute('/menu', renderMenu);
registerRoute('/tienda/:slug', renderTiendaCliente);

initRouter();
