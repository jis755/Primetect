using Npgsql;

namespace PrimeTect.Api.Tests;

/// <summary>
/// Pure (no network I/O) safety checks for the opt-in PostgreSQL verification suite.
/// <para>
/// Two different connections are involved and they must not be confused:
/// </para>
/// <list type="bullet">
/// <item>the <b>provisioning</b> connection, which may target only a local server and the administrative
/// <c>postgres</c>/<c>template1</c> database so it can create/drop the disposable test database; and</item>
/// <item>the <b>application/test</b> connection, which must target exactly the generated
/// <c>primetect_verification_&lt;guid&gt;</c> database.</item>
/// </list>
/// <para>
/// Each check is performed before any socket is opened, so a misconfigured target such as <c>primetect</c>, an
/// existing demo database, or the provisioning database itself is refused rather than connected to. The checks are
/// intentionally independent of a running server so they can be unit-tested without touching a database.
/// </para>
/// </summary>
public static class PostgresVerificationGuard
{
    public const string GeneratedDatabasePrefix = "primetect_verification_";

    /// <summary>True when the provisioning connection is safe to use for creating disposable databases.</summary>
    public static bool IsSafeLocalAdminTarget(NpgsqlConnectionStringBuilder builder)
    {
        var database = builder.Database?.Trim();
        var host = builder.Host?.Trim().ToLowerInvariant();
        return (string.Equals(database, "postgres", StringComparison.OrdinalIgnoreCase)
                || string.Equals(database, "template1", StringComparison.OrdinalIgnoreCase))
            && (host is "localhost" or "127.0.0.1" or "::1")
            && !string.IsNullOrWhiteSpace(builder.Username);
    }

    /// <summary>True when the name is exactly a generated <c>primetect_verification_&lt;32 hex&gt;</c> database.</summary>
    public static bool IsSafeGeneratedDatabaseName(string? name)
        => !string.IsNullOrEmpty(name)
           && name.StartsWith(GeneratedDatabasePrefix, StringComparison.Ordinal)
           && name.Length == GeneratedDatabasePrefix.Length + 32
           && name[GeneratedDatabasePrefix.Length..].All(Uri.IsHexDigit);

    /// <summary>
    /// Asserts that the effective application/test connection targets exactly <paramref name="expectedDatabaseName"/>
    /// and that the name is a generated disposable database. Throws before a connection is opened otherwise.
    /// </summary>
    public static void EnsureApplicationTarget(string connectionString, string expectedDatabaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database?.Trim();
        if (!string.Equals(database, expectedDatabaseName, StringComparison.Ordinal) || !IsSafeGeneratedDatabaseName(database))
        {
            throw new InvalidOperationException(
                "The effective PostgreSQL application/test connection must target only the generated disposable " +
                $"database '{expectedDatabaseName}'; refusing to open a connection to '{database ?? "(none)"}'.");
        }
    }
}
