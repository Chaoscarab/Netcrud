# NODE/EXPRESS → ASP.NET CORE CONVERSION GUIDE

Companion to `BACKEND_LIBRARIES.txt`. That file lists *what* is used. This file
maps each piece to its **Express + Prisma + TypeScript** equivalent so you can
reason about the app using patterns you already know.

Source of truth: `Backend/*.cs` (excluding `bin/` and `obj/`).

---

## 0. THE ONE-PARAGRAPH SUMMARY

Your Node mental model is:

> define schema → generate types → write service functions → wire middleware →
> mount routes → plug in an auth library.

ASP.NET Core is the same five steps, but the framework supplies the pieces that
you normally npm-install. There is no `prisma generate` because the **C# class
is the schema**. There is no `tsc` step separate from the build because it is one
compiler. There is no `express()` app object because `WebApplication` is both the
DI container and the middleware pipeline. And here there is no Passport, because
this project **hand-rolled sessions** — which is the part you most need to
understand for security.

---

## 1. PROJECT LAYOUT, SIDE BY SIDE

| Node/Express project | This project | Purpose |
|---|---|---|
| `package.json` | `Backend/Backend.csproj` | dependencies + target runtime |
| `node_modules/` | NuGet global cache (`~/.nuget`) | downloaded packages |
| `tsconfig.json` | `<Nullable>` / `<ImplicitUsings>` in `.csproj` | compiler strictness |
| `.env` | `appsettings.json` + `appsettings.Development.json` | config |
| `src/index.ts` (app bootstrap) | `Backend/Program.cs` | wiring + startup |
| `prisma/schema.prisma` | `Backend/Models/*.cs` + `Data/AppDbContext.cs` | data model |
| `prisma/migrations/` | `Backend/Migrations/` | schema history |
| `src/db.ts` (`new PrismaClient()`) | `Data/AppDbContext.cs` | DB client |
| `src/types/*.ts` (interfaces) | `Models/AuthDtos.cs`, nested `record`s | wire contracts |
| `src/routes/*.ts` (`express.Router()`) | `Controllers/*.cs` | HTTP endpoints |
| `src/middleware/auth.ts` | `Middleware/SessionAuthMiddleware.cs` | request gate |
| `src/services/*.ts` | `Services/PasswordService.cs`, `SessionService.cs` | business logic |
| `dist/` | `bin/Debug/net10.0/` | build output |
| `public/` served by `express.static` | `Backend/wwwroot/` | React build |

Key structural difference: **`obj/` and `bin/` are generated.** Treat them like
`node_modules/` + `dist/` — never edit, never commit.

---

## 2. STEP 1 — DEFINE THE DATA IN THE DATABASE

### Node: Prisma schema is the source of truth

```prisma
model Product {
  id       Int     @id @default(autoincrement())
  name     String  @db.VarChar(120) @unique
  quantity Int
  price    Decimal
}
```

Then `prisma generate` produces the TS type, and you import it.

### .NET: the C# class IS the schema ("code-first")

`Backend/Models/Product.cs`:

```csharp
public class Product
{
    public int Id { get; set; }              // "Id" -> primary key by convention

    [MaxLength(120)]                          // -> varchar(120)
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

There is **no generate step**. The class you write is simultaneously the table
definition *and* the type you use in code. This is the single biggest mental shift.

**Conventions replace decorators.** EF infers a lot without attributes:

| Prisma | EF Core equivalent |
|---|---|
| `@id @default(autoincrement())` | a property named `Id` (or `<Type>Id`) of type `int` |
| `@db.VarChar(120)` | `[MaxLength(120)]` |
| `String` (non-null) | `string` with `<Nullable>enable</Nullable>` |
| `String?` | `string?` |
| `@relation(...)` | navigation property + `HasOne/WithMany` in `OnModelCreating` |
| `@unique` | `HasIndex(...).IsUnique()` in `OnModelCreating` |

### The three-way relationship model

`Backend/Models/AppUser.cs` and `UserSession.cs` express a one-to-many:

```csharp
// AppUser.cs
public List<UserSession> Sessions { get; set; } = [];   // "many" side

// UserSession.cs
public int UserId { get; set; }        // the FK column
public AppUser? User { get; set; }     // the navigation property
```

This is Prisma's `sessions Session[]` / `user User @relation(fields: [userId], ...)`
split across two files. `UserId` is the actual column; `User` is the object you
get when you `Include(...)` (Prisma's `include: { user: true }`).

### Where the constraints actually live

`Data/AppDbContext.cs` → `OnModelCreating`. This is the Fluent API, the part of
Prisma's schema that has no attribute equivalent:

```csharp
modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
modelBuilder.Entity<Product>().HasIndex(p => p.Name).IsUnique();
modelBuilder.Entity<UserSession>().HasIndex(s => s.SessionKeyHash).IsUnique();

modelBuilder.Entity<UserSession>()
    .HasOne(s => s.User)
    .WithMany(u => u.Sessions)
    .HasForeignKey(s => s.UserId)
    .OnDelete(DeleteBehavior.Cascade);   // = onDelete: Cascade
```

**Security relevance:** the unique index on `Email` is what makes the signup
duplicate check *actually* safe under concurrency. The `AnyAsync` check in the
controller is a UX nicety; the database constraint is the real guarantee.

### `DbContext` = `PrismaClient`

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
}
```

`DbSet<Product>` ≈ `prisma.product`. `db.Products` ≈ `prisma.product`.

---

## 3. STEP 2 — MIGRATIONS

| Task | Prisma | EF Core |
|---|---|---|
| Create migration | `npx prisma migrate dev --name add_qty` | `dotnet ef migrations add AddProductQuantity` |
| Apply to DB | (same command) | `dotnet ef database update` |
| Apply in prod | `npx prisma migrate deploy` | `dotnet ef database update` |
| Undo last | `migrate resolve` / manual | `dotnet ef migrations remove` |
| Inspect state | `prisma migrate status` | `dotnet ef migrations list` |

Run these from the `Backend/` folder:

```powershell
cd Backend
dotnet ef migrations add MyChange
dotnet ef database update
```

Your existing history in `Backend/Migrations/`:

1. `20260729015016_InitialProducts` — Products table
2. `20260729020541_AddAuthAndSessions` — Users + Sessions + indexes
3. `20260802181139_AddProductQuantityAndUniqueName` — Quantity column + unique Name

**`AppDbContextModelSnapshot.cs`** has no Prisma equivalent. It is EF's cached
picture of "what the model looked like after the last migration." EF diffs your
current classes against this snapshot to generate the next migration. Never edit
it by hand; if it drifts, migrations generate garbage.

### Why `Data/AppDbContextFactory.cs` exists

`dotnet ef` needs a `DbContext` but must not boot your web server (which would
open ports and run middleware). `IDesignTimeDbContextFactory<AppDbContext>` is
the hook that builds a context for CLI tooling only. In Node you never hit this
because Prisma CLI reads `schema.prisma` directly and never touches your app code.

---

## 4. STEP 3 — TYPES FOR DATA IN AND OUT

### The DTO discipline (this is the security-critical part)

In Express you'd write:

```ts
interface SignUpRequest {
  firstName: string; lastName: string; email: string;
  password: string; confirmPassword: string;
}
interface UserResponse { id: number; firstName: string; lastName: string; email: string; }
```

`Backend/Models/AuthDtos.cs` is the exact same idea using **records**:

```csharp
public sealed record SignUpRequest(
    string FirstName, string LastName, string Email,
    string Password, string ConfirmPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(int Id, string FirstName, string LastName, string Email);
```

A `record` is an immutable class with value equality and a compact constructor —
think `type X = { ... }` but as a real runtime type.

**Why this matters:** `AppUser` has a `PasswordHash` property. The controller
never returns `AppUser`; it returns `UserResponse`. That is a deliberate
projection so the hash can never leak into JSON. This is the equivalent of
Prisma's `select: { id, firstName, lastName, email }` — except the compiler
enforces it because `UserResponse` has no such field to populate.

The same principle protects the input side. `CreateProductRequest` (declared
inside `Controllers/api.cs`) is `(string Name, int Quantity, decimal Price)` —
it has **no `Id`**, so a client cannot post an `Id` and overwrite an arbitrary
row. That is **mass-assignment protection**, and it comes from the DTO shape,
not from a sanitizer. Never bind directly to an entity class.

### Model binding = `express.json()` + Zod, partially

`[FromBody] SignUpRequest request` does what `JSON.parse` + a validator does.
With `[ApiController]` + nullable reference types enabled, ASP.NET automatically
returns `400` with a problem-details body if a non-nullable field is missing —
no Zod needed for presence checks.

But it does **not** validate semantics. That is why `AuthController` and
`ProductsController` still hand-check things like:

```csharp
if (request.Password != request.ConfirmPassword) return BadRequest(...);
if (request.Quantity < 0) return BadRequest(...);
```

For richer rules you would add `[Required]`, `[Range]`, `[StringLength]`
attributes to the DTO — that is the DataAnnotations equivalent of a Zod schema.

### Return types

| Express | ASP.NET Core |
|---|---|
| `res.json(x)` | `return Ok(x);` |
| `res.status(400).json({...})` | `return BadRequest(new { message = "..." });` |
| `res.status(409)` | `return Conflict(...)` |
| `res.status(401)` | `return Unauthorized(...)` |
| `res.status(404).end()` | `return NotFound();` |
| `res.status(204).end()` | `return NoContent();` |

`ActionResult<Product>` is the type that says "either a `Product` serialized to
JSON, or one of those status results." `IActionResult` is the untyped version.

---

## 5. STEP 4 — DEPENDENCY INJECTION (NO EXPRESS EQUIVALENT)

This has no Node counterpart and is the second big mental shift.

In Express you do this:

```ts
// db.ts
export const prisma = new PrismaClient();
// route.ts
import { prisma } from "../db";
```

A module-level singleton, imported wherever needed. ASP.NET instead uses a
**container**: you register what exists, and classes *declare* what they need in
their constructor. The framework constructs them.

`Program.cs` registration phase:

```csharp
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDataProtection();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddControllers();
```

Consumption, in `Controllers/AuthController.cs`:

```csharp
public class AuthController(AppDbContext db, PasswordService passwordService, SessionService sessionService)
    : ControllerBase
```

Nobody calls `new AuthController(...)`. The container sees the constructor,
resolves each parameter, and builds the object per request.

### Lifetimes — the rule you must internalize

| Lifetime | Meaning | Node analogy |
|---|---|---|
| `AddSingleton` | one instance for the process | module-level `const` |
| `AddScoped` | one instance **per HTTP request** | object created in a middleware and hung off `req` |
| `AddTransient` | new instance every injection | calling a factory each time |

`AppDbContext` is **scoped** and this is not optional. It holds a change tracker
and is not thread-safe. Because `AuthController` and `SessionService` are both
scoped, they receive **the same** `AppDbContext` instance inside one request — so
`db.Users.Add(user)` in the controller and `db.Sessions.Add(session)` in the
service participate in the same unit of work.

The trap: injecting a scoped service into a singleton. The container throws
("Cannot consume scoped service from singleton") — and if you dodge it manually
you get one `DbContext` alive forever, leaking tracked entities across users.
Coming from Node, the instinct to make things module-level singletons is exactly
the instinct to suppress here.

---

## 6. STEP 5 — MIDDLEWARE AND THE PIPELINE

### The shape is identical, the syntax is not

```ts
// Express
app.use(express.static("public"));
app.use(authMiddleware);
app.use("/api/products", productsRouter);
app.listen(3000);
```

```csharp
// Program.cs
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<SessionAuthMiddleware>();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
```

`app.UseX()` ≈ `app.use()`. `app.MapX()` ≈ mounting a router. **Order is
significant in both.** Static files are served before the auth middleware, which
is why the React bundle loads for logged-out users while `/api/*` stays gated.

### Writing a middleware

Express:

```ts
async function auth(req, res, next) {
  const cookie = req.cookies["netcrud_session"];
  if (!cookie) return res.sendStatus(401);
  const result = await validate(cookie);
  if (!result.valid) return res.sendStatus(401);
  (req as any).userId = result.userId;
  next();
}
```

`Middleware/SessionAuthMiddleware.cs`:

```csharp
public class SessionAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SessionService sessionService)
    {
        // ...
        if (!context.Request.Cookies.TryGetValue(SessionService.CookieName, out var cookieValue)
            || string.IsNullOrWhiteSpace(cookieValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;                       // <-- not calling next() = short-circuit
        }

        var result = await sessionService.ValidateCookieAsync(cookieValue, context.RequestAborted);
        if (!result.IsValid)
        {
            context.Response.Cookies.Delete(SessionService.CookieName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["UserId"] = result.UserId;
        await next(context);              // <-- next()
    }
}
```

Translation table:

| Express | ASP.NET Core |
|---|---|
| `req` + `res` | one `HttpContext context` |
| `next()` | `await next(context)` |
| not calling `next()` | `return;` without calling next |
| `req.cookies.x` | `context.Request.Cookies.TryGetValue("x", out var v)` |
| `res.clearCookie("x")` | `context.Response.Cookies.Delete("x")` |
| `res.sendStatus(401)` | `context.Response.StatusCode = StatusCodes.Status401Unauthorized;` |
| `(req as any).userId = 5` | `context.Items["UserId"] = 5` |
| `req.aborted` signal | `context.RequestAborted` (a `CancellationToken`) |

Two things worth calling out:

1. **`SessionService` is a parameter of `InvokeAsync`, not the constructor.**
   Middleware objects are effectively singletons — constructed once — so a scoped
   service must be injected *per invocation* via the method signature. This is
   a real footgun; putting `AppDbContext` in a middleware constructor gives you
   one context for the life of the app.

2. **`CancellationToken` has no Express equivalent you'd normally use.** Passing
   `context.RequestAborted` down into EF calls means that if the client hangs up,
   the database query is cancelled instead of running to completion. Free
   resilience; propagate it everywhere.

### The allow-list pattern

```csharp
private static readonly HashSet<string> PublicApiPaths =
[
    "/api/auth/signup",
    "/api/auth/login"
];
```

This is **deny-by-default**: every `/api/*` route requires a session unless
explicitly listed. That is the correct direction. The Express habit of
`app.use("/api/private", auth)` is allow-by-default, and forgetting to attach the
middleware to a new router silently exposes it. Keep this inversion.

Note `NormalizePath` lowercases and trims a trailing slash before the lookup, so
`/API/Auth/Login/` cannot slip past a case-sensitive comparison. Path-normalization
bugs are a classic auth-bypass class; this is why that helper exists.

---

## 7. STEP 6 — AUTH (NO PASSPORT / NEXTAUTH HERE)

**This app does not use an auth library.** There is no ASP.NET Identity, no
Passport equivalent. It implements opaque server-side sessions by hand. Understand
each layer, because you own all of it.

### Password storage — `Services/PasswordService.cs`

Replaces `bcrypt.hash` / `bcrypt.compare`.

```csharp
var salt = RandomNumberGenerator.GetBytes(16);          // CSPRNG, not Math.random
var key  = Rfc2898DeriveBytes.Pbkdf2(
    Encoding.UTF8.GetBytes(password), salt,
    100_000, HashAlgorithmName.SHA256, 32);

return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
```

Stored format is `iterations:base64Salt:base64Key` — self-describing, so raising
the iteration count later doesn't invalidate old hashes (verification reads the
iteration count *from the stored string*). That's the same trick bcrypt's `$2b$12$`
prefix plays.

Verification uses:

```csharp
CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
```

Not `==`. A normal comparison returns early on the first differing byte, which
leaks information through timing. `bcrypt.compare` does this for you; here it is
explicit. **Never compare a secret with `==`.**

### Session tokens — `Services/SessionService.cs`

The flow, end to end:

1. **Create** (on signup/login):
   - `rawSessionKey` = 32 CSPRNG bytes, hex-encoded.
   - Store `SHA256(rawSessionKey)` in the `Sessions` table — **never the raw key**.
   - Return `_protector.Protect(rawSessionKey)` — an *encrypted* value — to be
     written into the cookie.

2. **Validate** (every gated request):
   - `_protector.Unprotect(cookie)` — throws if tampered → treated as invalid.
   - Hash the recovered key, look up by hash.
   - If expired, delete the row and reject.

3. **Revoke** (logout): delete the row, delete the cookie.

Three properties fall out of this design:

- **Database leak ≠ account takeover.** The table holds hashes; you cannot replay
  a hash as a cookie. Same reasoning as password hashing, applied to tokens.
- **Sessions are revocable.** Deleting the row instantly kills the session. This
  is the big advantage over a stateless JWT, where you cannot revoke before expiry
  without inventing a blocklist.
- **Cookie tampering fails closed.** `Unprotect` throws on any modification, and
  the `catch` returns `Invalid`.

`Microsoft.AspNetCore.DataProtection` ≈ `cookie-parser`'s signed cookies or
`iron-session`, but encrypting rather than just signing.

> Production note: by default DataProtection keys are written to the local
> filesystem/registry. In Docker or across multiple instances, keys must be
> persisted to a shared store (`PersistKeysToFileSystem` + a mounted volume, or
> Redis/Azure Blob) or every restart/instance invalidates all sessions.

### Cookie flags — `AuthController.SetSessionCookie`

```csharp
new CookieOptions
{
    HttpOnly  = true,                       // JS cannot read it -> XSS can't steal it
    Secure    = Request.IsHttps,            // HTTPS-only when served over TLS
    SameSite  = SameSiteMode.Lax,           // baseline CSRF protection
    Expires   = DateTimeOffset.UtcNow.AddDays(7),
    IsEssential = true                      // exempt from cookie-consent suppression
}
```

Identical semantics to `res.cookie(name, value, { httpOnly, secure, sameSite })`.

`SameSite=Lax` blocks the cookie on cross-site POST/PUT/DELETE, which covers the
common CSRF cases. It does **not** cover cross-site top-level GET navigation — so
never put a state-changing action behind a GET. `GET /api/products` here is a pure
read, which is why the app is safe without CSRF tokens.

### The `HttpContext.Items["UserId"]` handoff

Middleware writes it, controller reads it:

```csharp
if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not int userId)
    return NoContent();
```

Exactly `req.userId` in Express, but untyped (`object`), which is why the
`is not int` pattern-match is needed. The idiomatic ASP.NET alternative is
`ClaimsPrincipal` / `User.FindFirst(...)` with `[Authorize]` attributes — worth
migrating to if you ever add roles.

### Login is deliberately vague

```csharp
if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
    return Unauthorized(new { message = "Invalid credentials." });
```

One message for both "no such email" and "wrong password" — prevents **user
enumeration**. Note the tension: signup *does* reveal existence via `409 Conflict`.
That's a common, accepted trade-off, but be aware you've made it.

---

## 8. QUERYING — PRISMA VS EF CORE

| Prisma | EF Core (in this app) |
|---|---|
| `prisma.product.findMany()` | `await db.Products.ToListAsync()` |
| `findUnique({ where: { id } })` | `await db.Products.FirstOrDefaultAsync(p => p.Id == id)` |
| `findFirst({ where: { email } })` | `await db.Users.FirstOrDefaultAsync(u => u.Email == email)` |
| `count() > 0` / `findFirst != null` | `await db.Users.AnyAsync(u => u.Email == email)` |
| `where: { quantity: { gt: 0 } }` | `.Where(p => p.Quantity > 0)` |
| `orderBy: { name: "asc" }` | `.OrderBy(p => p.Name)` |
| `include: { user: true }` | `.Include(s => s.User)` |
| `select: { id: true }` | `.Select(p => new { p.Id })` |
| `create({ data })` | `db.Products.Add(product); await db.SaveChangesAsync();` |
| `delete({ where: { id } })` | `db.Products.Remove(product); await db.SaveChangesAsync();` |
| `$transaction([...])` | multiple changes + one `SaveChangesAsync()` |

### The unit-of-work difference

Prisma writes immediately: `create()` hits the DB. EF **batches**. `Add` and
`Remove` only stage changes in the change tracker; nothing reaches Postgres until
`SaveChangesAsync()`, which wraps all staged changes in a single transaction. So

```csharp
db.Users.Add(user);
await db.SaveChangesAsync();     // <- INSERT happens here, and user.Id is populated
```

`user.Id` is `0` before that line and the real identity value after — EF reads it
back. That's how `CreateSessionCookieValueAsync(user.Id, ...)` on the next line works.

### Tracking — no Prisma equivalent

- `.AsNoTracking()` — read-only. Skips snapshotting each row for change detection.
  Faster, less memory. Use for anything you will not modify. Used for the product
  list and the `/me` lookup.
- `.AsTracking()` — used in `ValidateCookieAsync` specifically because an expired
  session may need `db.Sessions.Remove(session)` immediately after.

Coming from Prisma, the safe default is: **`AsNoTracking()` unless you intend to
write.**

### SQL injection

Both are safe by construction. `.Where(p => p.Quantity > 0)` is an expression tree
compiled to a parameterized query — the same guarantee Prisma gives. The moment
you reach for `db.Database.ExecuteSqlRaw($"... {input}")` with string
interpolation, you lose it. (`ExecuteSqlInterpolated` is the safe variant — it
parameterizes. `FromSqlRaw` with a concatenated string is the dangerous one.)

---

## 9. CONFIG AND STARTUP

`WebApplication.CreateBuilder(args)` layers configuration in this order, each
overriding the last:

1. `appsettings.json`
2. `appsettings.Development.json`
3. User Secrets (dev only)
4. Environment variables
5. Command-line `args`

So the Node pattern `process.env.DATABASE_URL || config.dbUrl` is built in.
`builder.Configuration.GetConnectionString("DefaultConnection")` reads
`ConnectionStrings:DefaultConnection`, and an env var named
`ConnectionStrings__DefaultConnection` (double underscore) overrides the JSON —
that is how `docker-compose.yml` injects it.

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
```

Fail-fast at boot rather than `undefined` surfacing at first query. Good pattern —
keep it.

**Secrets rule (same as Node):** never commit real credentials to
`appsettings.json`. Use `dotnet user-secrets` locally and environment variables in
production. `appsettings.json` is your committed `.env.example`, not your `.env`.

### Two-phase startup

Everything before `builder.Build()` is **registration** (what services exist).
Everything after is **pipeline** (how a request flows). You cannot add services
after `Build()`. Express has no such split, and it's a common early confusion.

---

## 10. COMMAND CHEAT SHEET

| Task | Node | This project |
|---|---|---|
| Install deps | `npm install` | `dotnet restore` (implicit in build) |
| Add a package | `npm i pkg` | `dotnet add package Pkg` |
| Dev server | `npm run dev` | `dotnet run` (from `Backend/`) |
| Watch mode | `nodemon` / `tsx watch` | `dotnet watch run` |
| Type-check | `tsc --noEmit` | `dotnet build` |
| Prod build | `tsc` | `dotnet publish -c Release` |
| New migration | `prisma migrate dev --name x` | `dotnet ef migrations add X` |
| Apply migration | `prisma migrate deploy` | `dotnet ef database update` |
| Frontend build | `npm run build` | same (in `Frontend/`), output copied to `Backend/wwwroot/` |

---

## 11. SECURITY CHECKLIST FOR YOUR NEXT CRUD APP

What this codebase already does right — reuse these:

- [x] Passwords hashed with a slow KDF (PBKDF2, 100k iterations) + per-user salt
- [x] Constant-time hash comparison
- [x] Session tokens from a CSPRNG, stored **hashed**, encrypted in the cookie
- [x] `HttpOnly` + `Secure` + `SameSite=Lax` cookies
- [x] Deny-by-default middleware with an explicit public allow-list
- [x] Path normalization before the allow-list check
- [x] Separate request/response DTOs — no entity binding, no hash leakage
- [x] Parameterized queries via LINQ
- [x] Uniform "Invalid credentials." to prevent user enumeration
- [x] DB-level unique constraints backing the app-level checks
- [x] Server-side sessions (revocable) instead of unrevocable JWTs

Gaps in this app that you should close in production work:

- [ ] **No per-user ownership on products.** `ProductsController` authenticates
      but never authorizes — any logged-in user can delete any product. There is
      no `OwnerId` on `Product` and no `.Where(p => p.OwnerId == userId)`. This is
      OWASP **A01: Broken Access Control**, the most common real-world flaw.
      Authentication answers *who*; authorization answers *what they may touch*.
- [ ] **No rate limiting on login/signup.** Add `builder.Services.AddRateLimiter(...)`
      + `app.UseRateLimiter()` (≈ `express-rate-limit`). Without it, PBKDF2 only
      slows an offline attack, not an online one.
- [ ] **`DbUpdateException` on the unique index is unhandled.** Two concurrent
      signups with the same email pass `AnyAsync` and one throws a 500. Wrap
      `SaveChangesAsync` and translate the constraint violation to `409`.
- [ ] **Case-sensitivity mismatch on product names.** `Create` compares with
      `.ToLower()` but the unique index is on the raw `Name`, so `"Widget"` and
      `"widget"` both pass the check *and* both insert. Use `citext` or a computed
      lowercase column so the index matches the check.
- [ ] **No global exception handler.** Add `app.UseExceptionHandler(...)` so raw
      stack traces never reach a client.
- [ ] **`UseHttpsRedirection` is dev-only.** Intentional if TLS terminates at a
      reverse proxy, but then add `app.UseForwardedHeaders()` so `Request.IsHttps`
      is accurate — otherwise the cookie's `Secure` flag silently turns off.
- [ ] **No security headers.** No HSTS, CSP, or `X-Content-Type-Options`.
- [ ] **Expired sessions are only cleaned on access.** Add a background
      `IHostedService` sweep, or the table grows forever.
- [ ] **No max session count per user** and no session rotation on privilege change.
- [ ] **DataProtection keys are not persisted** — see the note in §7.

---

## 12. THE MENTAL MODEL, RESTATED

| Your Node step | The equivalent here |
|---|---|
| 1. Define DB data | Write entity classes in `Models/`, constrain in `OnModelCreating`, `dotnet ef migrations add` |
| 2. Type the data in/out | `record` DTOs — separate from entities, always |
| 3. Write functions/interfaces | Classes in `Services/`, registered with `AddScoped` |
| 4. Middleware + routes | `app.UseX()` for the pipeline, `[ApiController]` classes for routes |
| 5. Connect the DB | `AddDbContext<T>(o => o.UseNpgsql(...))`, then constructor-inject |
| 6. Auth library | Hand-rolled here: `PasswordService` + `SessionService` + `SessionAuthMiddleware` |

The rhythm is the same. What changes: **classes replace the schema file**, **the
DI container replaces module imports**, and **the framework replaces about six
npm packages** (body parsing, cookie signing, static serving, routing, config,
validation).
