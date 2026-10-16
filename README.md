# Bean & Bloom — Coffee Shop Management

A small React/JavaScript frontend and ASP.NET Core 8 API with Entity Framework Core and **Microsoft SQL Server**. MSSQL follows the detailed requirements; SQLite is not used.

## Requirements

- .NET 8 SDK (or a newer SDK capable of targeting .NET 8)
- Node.js 22+ and npm
- SQL Server Developer or Express, running locally, with Windows authentication

## Database setup

The default connection in `backend/appsettings.json` is:

```text
Server=localhost;Database=CoffeeShop;Trusted_Connection=True;TrustServerCertificate=True
```

Your Windows account needs permission to create a database. The API creates the `CoffeeShop` database and tables on its first start using EF Core `EnsureCreated`, then inserts six sample coffees. Development mode also seeds the accounts below when the users table is empty. No manual SQL import is needed.

For SQL Express, change the server to `localhost\\SQLEXPRESS` in the JSON string. Alternatively override the whole connection without editing tracked settings:

```powershell
$env:ConnectionStrings__CoffeeShop = 'Server=localhost;Database=CoffeeShop;Trusted_Connection=True;TrustServerCertificate=True'
```

For SQL authentication, supply `User Id` and `Password` through this environment variable instead of `Trusted_Connection`. Do not commit credentials. `TrustServerCertificate=True` is for local development; deployed environments should use a trusted SQL certificate.

This small project uses `EnsureCreated`, not EF migrations. It creates a fresh schema but does not upgrade an existing schema after model changes. Use a new database name during development, or introduce EF migrations before evolving a database containing important data.

## Run locally

From the project root, in the first PowerShell terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/CoffeeShop.Api.csproj --launch-profile CoffeeShop.Api
```

In a second terminal:

```powershell
cd frontend
npm.cmd install
npm.cmd run dev
```

Open **http://127.0.0.1:5173**. On macOS/Linux use `npm` instead of `npm.cmd` and an accessible SQL Server connection. Vite proxies `/api` to **http://127.0.0.1:65421**, the HTTP endpoint in the backend launch profile. Cookie authentication works through the same frontend origin without CORS configuration.

### Run the backend in Visual Studio

1. Install the **ASP.NET and web development** workload and an SDK supporting .NET 8.
2. Open `backend/CoffeeShop.Api.csproj` using **File > Open > Project/Solution**. Set it as the startup project if other projects are open.
3. Verify the SQL Server connection in `backend/appsettings.json` and start SQL Server.
4. Select the **CoffeeShop.Api** launch profile and press **F5** (debug) or **Ctrl+F5** (run without debugging). This profile sets the Development environment and exposes HTTP port **65421** and HTTPS port **65420**. Trust the development HTTPS certificate if prompted.
5. Check **http://127.0.0.1:65421/api/coffees**. It should return coffee data. The API root `/` has no page; use React for the interface.
6. Keep the backend running, start the frontend with `npm.cmd run dev` from `frontend`, and open **http://127.0.0.1:5173**.

Run the backend either from Visual Studio or from the terminal, not both on the same ports.

### Troubleshoot Vite `ECONNREFUSED`

This means Vite cannot connect to the backend. Starting Vite does not start ASP.NET Core.

- Start the **CoffeeShop.Api** backend and wait for it to listen on port **65421**. If startup fails, check the backend output for database or build errors.
- Confirm `http://127.0.0.1:65421/api/coffees` works directly.
- Ensure the `/api` proxy in `frontend/vite.config.js` targets `http://127.0.0.1:65421`. Port 5080 does not match the Visual Studio profile.
- Restart Vite after configuration changes, then reload the page.
- A **401** response from `/api/auth/me` before signing in is expected; it is different from a proxy connection error.

To test through the running frontend proxy:

```powershell
$env:API_URL = 'http://127.0.0.1:5173/api'
node scripts/smoke-test.mjs
```

### Development accounts

| Role | Email | Password |
| --- | --- | --- |
| Sales | sales@coffee.local | Coffee123! |
| Customer | customer@coffee.local | Coffee123! |

You can also register a new customer. Sales accounts cannot be created through public registration. Demo credentials are seeded only in Development mode. Do not expose this demo database publicly with these passwords.

## Using the app

- Guests can browse and search the menu, view coffee details, and build a coffee bag.
- Customers sign in or register to place an order, view their history, and refresh order status.
- Sales users see all orders, advance them through Pending → Confirmed → Preparing → Completed, and add or edit coffees including price, description, image URL, and stock.
- Stock is deducted when an order is placed. The API calculates prices and totals from the database. Atomic stock updates in a transaction prevent overselling and roll back failed purchases.
- Prices are in USD. Orders are for counter pickup, with no online payment integration.

## Structure

```text
backend/
  Program.cs       API routes, cookie authentication, authorization, seed data
  Models.cs        Entities, EF database context, request records
  appsettings.json Local database configuration
frontend/
  src/main.jsx     React screens, forms, cart, and API integration
  src/styles.css   Responsive coffee-themed interface
  vite.config.js   Development API proxy
scripts/
  smoke-test.mjs   Integration checks against the running API and MSSQL
```

## Verification

```powershell
dotnet build backend/CoffeeShop.Api.csproj
cd frontend
npm.cmd run build
cd ..
node scripts/smoke-test.mjs
```

Start the API before running the smoke test. The test checks real database writes, authentication, role authorization, order privacy, rollback, totals, status progression, and coffee edits. It restores the coffee it edits, but leaves a completed test order and test customer for inspection; run it only against a development database with no concurrent activity.

Coffee photos are served from Unsplash and fonts from Google Fonts, so these assets require internet access. The UI uses system fonts as fallback. The production frontend builds into `frontend/dist`; production hosting must forward `/api` to the API on the same origin and use HTTPS. Deployment and payment processing are outside this local project's scope.
