// Run against a fresh development database: node scripts/smoke-test.mjs
import assert from 'node:assert/strict';
const base = process.env.API_URL || 'http://127.0.0.1:65421/api';
function client() {
  let cookie = '';
  return async (path, method = 'GET', body, expected = 200) => {
    const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', Cookie: cookie }, body: body ? JSON.stringify(body) : undefined });
    if (r.headers.get('set-cookie')) cookie = r.headers.get('set-cookie').split(';')[0];
    const data = await r.text();
    assert.equal(r.status, expected, `${method} ${path}: ${data}`);
    return data ? JSON.parse(data) : null;
  };
}
const customer = client(), sales = client(), stranger = client();
await stranger('/orders', 'GET', null, 401);
await sales('/auth/login', 'POST', { email: 'sales@coffee.local', password: 'wrong' }, 401);
await sales('/auth/login', 'POST', { email: 'sales@coffee.local', password: 'Coffee123!' });
await customer('/auth/register', 'POST', { name: 'Smoke test', email: `smoke-${Date.now()}@example.com`, password: 'TestCoffee123!' });
const coffees = await customer('/coffees');
assert.equal(coffees.length >= 6, true);
const coffee = coffees.find(c => c.stock > 0);
await customer('/coffees', 'POST', coffee, 403);
await sales('/orders', 'POST', { items: [{ coffeeId: coffee.id, quantity: 1 }] }, 403);
await customer('/orders', 'POST', { items: [{ coffeeId: coffee.id, quantity: 0 }] }, 400);
await customer('/orders', 'POST', { items: [{ coffeeId: coffee.id, quantity: 1 }, { coffeeId: 999999, quantity: 1 }] }, 409);
assert.equal((await customer('/coffees')).find(c => c.id === coffee.id).stock, coffee.stock, 'Failed order rolls stock back');
const order = await customer('/orders', 'POST', { items: [{ coffeeId: coffee.id, quantity: 2 }] });
const ownOrders = await customer('/orders');
assert.equal(ownOrders.length, 1);
assert.equal(ownOrders[0].total, coffee.price * 2);
assert.equal((await customer('/coffees')).find(c => c.id === coffee.id).stock, coffee.stock - 2);
await stranger('/auth/login', 'POST', { email: 'customer@coffee.local', password: 'Coffee123!' });
assert.equal((await stranger('/orders')).some(o => o.id === order.id), false, 'Orders remain private');
await customer(`/orders/${order.id}/status`, 'PUT', { status: 'Confirmed' }, 403);
await sales(`/orders/${order.id}/status`, 'PUT', { status: 'Completed' }, 400);
for (const status of ['Confirmed', 'Preparing', 'Completed']) await sales(`/orders/${order.id}/status`, 'PUT', { status }, 204);
assert.equal((await customer('/orders'))[0].status, 'Completed');
await sales('/coffees/' + coffee.id, 'PUT', { ...coffee, description: coffee.description + ' Tested.' });
assert.equal((await sales('/coffees')).find(c => c.id === coffee.id).description, coffee.description + ' Tested.');
await sales('/coffees/' + coffee.id, 'PUT', coffee); // restore sample coffee and stock
await customer('/auth/logout', 'POST', null, 204);
await customer('/orders', 'GET', null, 401);
console.log('PASS: registration, login, permissions, validation, stock rollback, purchase totals, order privacy, status progression, coffee editing, logout.');
console.log(`Completed test order #${order.id} and its test account remain available for inspection.`);
