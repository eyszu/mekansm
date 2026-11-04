using Mekansm.Models;
using Mekansm.Services;
using Npgsql;
using NpgsqlTypes;

namespace Mekansm.Data;

public sealed class PostRepository(NpgsqlDataSource ds)
{
    private const string From = " from posts p join users u on u.id = p.author_id ";

    // List queries skip the (potentially large) markdown body.
    private static string Cols(bool withBody) =>
        "p.id, p.author_id, u.username, p.title, p.slug, p.excerpt, " +
        (withBody ? "p.body_md" : "''::text") +
        ", p.status, p.published_at, p.created_at, p.updated_at";

    private static Post Map(NpgsqlDataReader r) => new(
        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4),
        r.GetString(5), r.GetString(6), r.GetString(7),
        r.IsDBNull(8) ? null : r.GetDateTime(8), r.GetDateTime(9), r.GetDateTime(10));

    private static async Task<List<Post>> ReadAllAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Post>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    private static NpgsqlParameter TextParam(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    public async Task<List<Post>> ListPublishedAsync(int page, int size, string? authorUsername = null, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand(
            "select " + Cols(false) + From +
            "where p.status = 'published' and (@a is null or u.username = @a) " +
            "order by p.published_at desc, p.id desc limit @n offset @o");
        cmd.Parameters.Add(TextParam("a", authorUsername));
        cmd.Parameters.AddWithValue("n", size);
        cmd.Parameters.AddWithValue("o", (page - 1) * size);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<int> CountPublishedAsync(string? authorUsername = null, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand(
            "select count(*)" + From + "where p.status = 'published' and (@a is null or u.username = @a)");
        cmd.Parameters.Add(TextParam("a", authorUsername));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<List<Post>> ListByAuthorAsync(long authorId, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand(
            "select " + Cols(false) + From + "where p.author_id = @a order by p.updated_at desc");
        cmd.Parameters.AddWithValue("a", authorId);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<Post?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand("select " + Cols(true) + From + "where p.slug = @s");
        cmd.Parameters.AddWithValue("s", slug);
        var list = await ReadAllAsync(cmd, ct);
        return list.FirstOrDefault();
    }

    public async Task<Post?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand("select " + Cols(true) + From + "where p.id = @id");
        cmd.Parameters.AddWithValue("id", id);
        var list = await ReadAllAsync(cmd, ct);
        return list.FirstOrDefault();
    }

    public async Task<(long Id, string Slug)> CreateAsync(
        long authorId, string title, string excerpt, string body, bool publish, CancellationToken ct = default)
    {
        var baseSlug = Slugger.Make(title);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var slug = attempt == 0 ? baseSlug : $"{baseSlug}-{Random.Shared.Next(1000, 10000)}";
            try
            {
                await using var cmd = ds.CreateCommand(
                    "insert into posts (author_id, title, slug, excerpt, body_md, status, published_at) " +
                    "values (@a, @t, @s, @x, @b, @st, case when @st = 'published' then now() else null end) " +
                    "returning id");
                cmd.Parameters.AddWithValue("a", authorId);
                cmd.Parameters.AddWithValue("t", title);
                cmd.Parameters.AddWithValue("s", slug);
                cmd.Parameters.AddWithValue("x", excerpt);
                cmd.Parameters.AddWithValue("b", body);
                cmd.Parameters.AddWithValue("st", publish ? "published" : "draft");
                var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
                return (id, slug);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // slug collision: retry with a suffix
            }
        }

        throw new InvalidOperationException("Could not allocate a unique slug.");
    }

    /// <summary>Updates a post owned by <paramref name="authorId"/>. The slug stays stable.</summary>
    public async Task<bool> UpdateAsync(
        long id, long authorId, string title, string excerpt, string body, bool publish, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand(
            "update posts set title = @t, excerpt = @x, body_md = @b, status = @st, " +
            "published_at = case when @st = 'published' then coalesce(published_at, now()) else published_at end, " +
            "updated_at = now() where id = @id and author_id = @a");
        cmd.Parameters.AddWithValue("t", title);
        cmd.Parameters.AddWithValue("x", excerpt);
        cmd.Parameters.AddWithValue("b", body);
        cmd.Parameters.AddWithValue("st", publish ? "published" : "draft");
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("a", authorId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(long id, long authorId, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand("delete from posts where id = @id and author_id = @a");
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("a", authorId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
