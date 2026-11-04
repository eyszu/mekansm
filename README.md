# mekansm

A minimal blog platform. Server-rendered ASP.NET Core Razor Pages on .NET 8, PostgreSQL through raw Npgsql (no ORM), Markdown posts.

> Status: the source has not been compiled or run yet. Expect to fix small build errors on first build.

## Stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 8, ASP.NET Core Razor Pages (server-side rendering, no WASM) |
| Database | PostgreSQL 16, `Npgsql` 10 (`NpgsqlDataSource`, hand-written SQL) |
| Markdown | `Markdig` 0.40 (advanced extensions, raw HTML disabled) |
| HTML sanitizing | `HtmlSanitizer` 9 (Ganss.Xss) on every render |
| Auth | Cookie authentication, PBKDF2-SHA256 password hashes |
| Frontend | Plain CSS and vanilla JS, no build step |

## Run

```bash
docker compose up -d     # postgres:16 on :5432, db/user/password all "mekansm"
dotnet run               # applies the schema on startup
```

Configuration (`appsettings.json` or environment variables):

| Key | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Default` (`ConnectionStrings__Default`) | local `mekansm` database | Npgsql connection string |
| `TrustForwardedHeaders` | `false` | Trust `X-Forwarded-For/Proto` from any proxy. Enable only behind a reverse proxy you control |

## Project layout

```
Program.cs            DI, auth, rate limiter, middleware, minimal API endpoints
Data/
  Schema.cs           idempotent DDL, run once at startup
  UserRepository.cs   user lookup and creation
  PostRepository.cs   post queries and writes
Services/
  PasswordService.cs  PBKDF2 hashing and verification
  MarkdownService.cs  Markdown to sanitized HTML, plain-text excerpts
  Slugger.cs          title to URL slug
  AuthHelper.cs       sign-in and user-id claim helpers
Models/Models.cs      AppUser, Post, Pager records
Pages/                Razor Pages (public, auth, Dashboard/)
wwwroot/              site.css, site.js, editor.js
```

Repositories and services are singletons. They hold no per-request state, and every query opens a pooled connection from the shared `NpgsqlDataSource`.

## Data model

Created by `Schema.EnsureAsync` with `create ... if not exists`. There is no migration tool, so schema changes after first deploy must be applied by hand.

```sql
users (
  id bigserial pk, email text, username text,
  password_hash text, created_at timestamptz default now()
)
  unique index ux_users_email    on lower(email)
  unique index ux_users_username on username

posts (
  id bigserial pk, author_id bigint fk users(id) on delete cascade,
  title text, slug text, excerpt text default '', body_md text default '',
  status text check in ('draft','published') default 'draft',
  published_at timestamptz null,
  created_at timestamptz default now(), updated_at timestamptz default now()
)
  unique index ux_posts_slug
  index ix_posts_published on (published_at desc) where status = 'published'
  index ix_posts_author    on (author_id, updated_at desc)
```

Behavior worth knowing:

- Emails are stored lowercased and matched case-insensitively.
- The username is derived from the email's local part (slugified, max 24 chars). On a collision it retries with a random 4-digit suffix, up to 8 attempts. The email unique-violation is told apart from the username one by constraint name.
- Slugs come from the title, are unique across all authors, and use the same suffix retry. A slug never changes after creation, even if the title is edited.
- `published_at` is set the first time a post is published and kept on later edits. Unpublishing keeps it.
- List queries select `''` instead of `body_md`, so listing pages never read post bodies.
- Update and delete include `author_id` in the `WHERE` clause, so ownership is enforced in SQL as well as in the page models.
- All SQL is parameterized. Nullable text filters use an explicitly typed `NpgsqlDbType.Text` parameter.

## Routes

| Route | Auth | Description |
| --- | --- | --- |
| `GET /` | no | Latest published posts, 12 per page (`?p=`) |
| `GET /p/{slug}` | no (drafts: owner only) | Post page. Drafts return 404 to anyone else |
| `GET /author/{username}` | no | Published posts by one author |
| `GET/POST /Register` | no | Email and password sign-up, then signs in |
| `GET/POST /Login` | no | Sign-in with optional persistent cookie and `ReturnUrl` |
| `GET/POST /Logout` | no | POST signs out |
| `GET /Dashboard` | yes | Own posts (drafts and published). `POST ?handler=Delete&id=` deletes |
| `GET/POST /Dashboard/Edit/{id:long?}` | yes | Create or edit a post |
| `POST /api/preview` | yes | JSON `{ "markdown": "..." }`, returns sanitized HTML. 200 KB limit |
| `GET /feed.xml` | no | RSS 2.0, latest 20 published posts |
| `/Error?code=` | no | Status-code page (re-executed for 404 and similar) |

`/Dashboard` is protected with `AuthorizeFolder`. Unauthenticated requests are redirected to `/Login?ReturnUrl=...`, and the return URL is checked with `Url.IsLocalUrl`.

## Authentication

- Cookie `mekansm.auth`: `HttpOnly`, `SameSite=Lax`, `Secure` when the request is HTTPS, 14-day sliding expiry. The "keep me signed in" box makes the cookie persistent, otherwise it is a session cookie. Registration always signs in persistently.
- Claims: `NameIdentifier` (user id), `Name` (username), `Email`.
- Password hash format: `v1.{iterations}.{saltB64}.{hashB64}`. PBKDF2-SHA256, 210,000 iterations, 16-byte salt, 32-byte hash, compared with `CryptographicOperations.FixedTimeEquals`. The iteration count is stored per hash, so it can be raised later without invalidating existing hashes.
- Login against an unknown email still runs a full PBKDF2 verification against a dummy hash, so response time does not reveal whether an email is registered. The error message is the same for both cases.
- Password rules: 8 to 128 characters. No complexity rules.
- Rate limit policy `auth` (Login and Register): fixed window, 10 requests per minute per client IP. Returns 429.

## Markdown pipeline

`MarkdownService.ToHtml`:

1. Markdig with `UseAdvancedExtensions()` (tables, task lists, footnotes, autolinks, definition lists, and others) and `DisableHtml()`, so raw HTML in the source is escaped.
2. The output goes through `HtmlSanitizer` with the default allow-list, plus `<input type=checkbox checked disabled>` for task lists.
3. Every `<a>` gets `rel="nofollow ugc noopener"`.

Posts are rendered on every view and nothing is cached. Because the stored value is the Markdown source, changing the pipeline re-renders old posts automatically. If traffic grows, cache the HTML per post id and `updated_at`.

Excerpts, when the author leaves the field empty, come from `Markdown.ToPlainText`, with whitespace collapsed and truncated to 180 characters.

The editor (`wwwroot/js/editor.js`) debounces input by 250 ms and posts to `/api/preview`. It discards out-of-order responses with a sequence counter and sends the antiforgery token in the `RequestVerificationToken` header. The preview HTML is trusted only because the server sanitized it.

## Security notes

- CSRF: Razor Pages validates antiforgery tokens on all POSTs by default. `/api/preview` validates the token manually.
- Response headers on every request: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a CSP of `default-src 'self'; img-src 'self' https: data:; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`. There are no inline scripts or styles, and the delete confirmation uses a `data-confirm` attribute handled in `site.js`.
- Post HTML is the only place `Html.Raw` is used, and it is always sanitized output.
- `X-Forwarded-*` headers are ignored unless `TrustForwardedHeaders=true`. This matters because the rate limiter keys on the client IP.
- No HTTPS redirect or HSTS is configured. Terminate TLS at your proxy or add `UseHttpsRedirection` and `UseHsts`.

## Known limitations

- No email verification and no password reset. Email is only a login identifier.
- No roles. Every user is an author with a dashboard, and nobody can moderate other users' posts.
- No comments, tags, categories, search, or media uploads. Images must be hosted elsewhere and linked (CSP allows any `https:` image).
- No schema migrations and no automated tests.
- Slugs and usernames can be taken by whoever registers first, with no reserved names.
- The in-memory rate limiter is per process. With several instances each keeps its own counters.

## Deploying

1. Provision PostgreSQL and set `ConnectionStrings__Default`.
2. Put the app behind a TLS-terminating reverse proxy (nginx, Caddy) and set `TrustForwardedHeaders=true`.
3. Run `dotnet publish -c Release` and run the output. The schema is applied on first start, so the database user needs `CREATE` rights at least once.
4. For multiple instances, share the ASP.NET Data Protection keys (for example `PersistKeysToFileSystem` on shared storage). Otherwise auth cookies and antiforgery tokens will not validate across instances or after a restart.