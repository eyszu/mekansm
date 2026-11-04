using System.Threading.RateLimiting;
using System.Xml.Linq;
using Mekansm.Data;
using Mekansm.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connectionString).Build());
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<PostRepository>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<MarkdownService>();

builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/Dashboard"));
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "mekansm.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Brute-force protection for login/registration: 10 requests per minute per IP.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// Set TrustForwardedHeaders=true only when running behind a reverse proxy you control.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (builder.Configuration.GetValue<bool>("TrustForwardedHeaders"))
    {
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    }
});

var app = builder.Build();

await Schema.EnsureAsync(app.Services.GetRequiredService<NpgsqlDataSource>());

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' https: data:; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Live markdown preview for the editor (rendered + sanitized server side).
app.MapPost("/api/preview", async (PreviewRequest req, HttpContext ctx, IAntiforgery antiforgery, MarkdownService md) =>
{
    try { await antiforgery.ValidateRequestAsync(ctx); }
    catch (AntiforgeryValidationException) { return Results.BadRequest(); }

    if (req.Markdown is { Length: > 200_000 }) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    return Results.Content(md.ToHtml(req.Markdown), "text/html; charset=utf-8");
}).RequireAuthorization();

// RSS feed of the latest published posts.
app.MapGet("/feed.xml", async (HttpContext ctx, PostRepository posts) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var items = await posts.ListPublishedAsync(1, 20);

    var channel = new XElement("channel",
        new XElement("title", "mekansm"),
        new XElement("link", baseUrl),
        new XElement("description", "Latest posts on mekansm"));

    foreach (var p in items)
    {
        var url = $"{baseUrl}/p/{p.Slug}";
        channel.Add(new XElement("item",
            new XElement("title", p.Title),
            new XElement("link", url),
            new XElement("guid", url),
            new XElement("pubDate", new DateTimeOffset(p.PublishedAt ?? p.CreatedAt).ToString("R")),
            new XElement("description", p.Excerpt)));
    }

    var doc = new XElement("rss", new XAttribute("version", "2.0"), channel);
    return Results.Content("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + doc, "application/rss+xml; charset=utf-8");
});

app.Run();

internal sealed record PreviewRequest(string? Markdown);
