# mekansm

A small blog platform. ASP.NET Core (.NET 8, Razor Pages), PostgreSQL via raw Npgsql (no ORM), Markdown posts.

## Run

```bash
docker compose up -d        # PostgreSQL 16 on :5432 (user/pass/db: mekansm)
dotnet run                  # schema is created automatically on startup
```

Open the URL printed by `dotnet run`, sign up with an email and password, and click **Write**.

Override the database with `ConnectionStrings__Default` (env var) or `appsettings.json`.

## Features

- Registration and login with email and password (PBKDF2-SHA256, 210k iterations, cookie auth, rate limited)
- Posts with draft/publish, stable slugs, excerpts, pagination, author pages, RSS (`/feed.xml`)
- Markdown (Markdig, GFM tables, task lists, footnotes) with live preview in the editor
- Raw HTML in Markdown is disabled and the output is sanitized (HtmlSanitizer)
- CSRF protection on all forms; security headers and a CSP
- Dark arcade theme inspired by wasmarcade.com

## Layout

- `Data/` plain SQL repositories and the idempotent schema
- `Services/` password hashing, Markdown rendering, slugs
- `Pages/` Razor Pages (public, auth, `Dashboard/` for the signed-in author)

## Behind a proxy

Set `TrustForwardedHeaders=true` only when running behind a reverse proxy you control. Serve over HTTPS in production.
