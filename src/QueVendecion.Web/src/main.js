import './style.css';
import { registerRoute, initRouter } from './utils/router.js';
import { renderHome } from './pages/home.js';
import { renderTienda } from './pages/tienda.js';
import { renderPedidos } from './pages/pedidos.js';
import { renderMenu } from './pages/menu.js';
import { renderInventario } from './pages/inventario.js';
import { renderLogin } from './pages/login.js';
import { renderTiendaCliente } from './pages/tiendaCliente.js';
import { renderSuperAdmin } from './pages/superAdmin.js';

registerRoute('/', renderHome);
registerRoute('/login', renderLogin);
registerRoute('/mi-tienda', renderTienda);
registerRoute('/pedidos', renderPedidos);
registerRoute('/menu', renderMenu);
registerRoute('/inventario', renderInventario);
registerRoute('/tienda/:slug', renderTiendaCliente);
registerRoute('/superadmin', renderSuperAdmin);

initRouter();
