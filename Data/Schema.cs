using Npgsql;

namespace Mekansm.Data;

/// <summary>Idempotent schema, applied on startup. Plain SQL, no ORM.</summary>
public static class Schema
{
    public const string Sql = """
        create table if not exists users (
            id            bigserial primary key,
            email         text not null,
            username      text not null,
            password_hash text not null,
            created_at    timestamptz not null default now()
        );
        create unique index if not exists ux_users_email    on users (lower(email));
        create unique index if not exists ux_users_username on users (username);

        create table if not exists posts (
            id           bigserial primary key,
            author_id    bigint not null references users(id) on delete cascade,
            title        text not null,
            slug         text not null,
            excerpt      text not null default '',
            body_md      text not null default '',
            status       text not null default 'draft' check (status in ('draft', 'published')),
            published_at timestamptz,
            created_at   timestamptz not null default now(),
            updated_at   timestamptz not null default now()
        );
        create unique index if not exists ux_posts_slug on posts (slug);
        create index if not exists ix_posts_published on posts (published_at desc) where status = 'published';
        create index if not exists ix_posts_author on posts (author_id, updated_at desc);
        """;

    public static async Task EnsureAsync(NpgsqlDataSource ds)
    {
        await using var cmd = ds.CreateCommand(Sql);
        await cmd.ExecuteNonQueryAsync();
    }
}
