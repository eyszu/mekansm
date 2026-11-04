using Mekansm.Models;
using Mekansm.Services;
using Npgsql;

namespace Mekansm.Data;

public sealed class UserRepository(NpgsqlDataSource ds)
{
    private const string Cols = "id, email, username, password_hash, created_at";

    private static AppUser Map(NpgsqlDataReader r) =>
        new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDateTime(4));

    public async Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand($"select {Cols} from users where lower(email) = lower(@e)");
        cmd.Parameters.AddWithValue("e", email);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<AppUser?> FindByIdAsync(long id, CancellationToken ct = default)
    {
        await using var cmd = ds.CreateCommand($"select {Cols} from users where id = @id");
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    /// <summary>
    /// Creates a user. The public username is derived from the email's local part.
    /// Returns null when the email is already registered.
    /// </summary>
    public async Task<AppUser?> CreateAsync(string email, string passwordHash, CancellationToken ct = default)
    {
        var baseName = Slugger.Make(email.Split('@')[0], 24, "user");

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var username = attempt == 0 ? baseName : $"{baseName}-{Random.Shared.Next(1000, 10000)}";
            try
            {
                await using var cmd = ds.CreateCommand(
                    $"insert into users (email, username, password_hash) values (@e, @u, @p) returning {Cols}");
                cmd.Parameters.AddWithValue("e", email);
                cmd.Parameters.AddWithValue("u", username);
                cmd.Parameters.AddWithValue("p", passwordHash);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                await r.ReadAsync(ct);
                return Map(r);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                if (ex.ConstraintName == "ux_users_email") return null;
                // otherwise the username was taken: loop and try a suffixed one
            }
        }

        throw new InvalidOperationException("Could not allocate a unique username.");
    }
}
