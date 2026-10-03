using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Data;

namespace PoE.Valuation.Infrastructure.Data;

/// <summary>
/// Creates PostgreSQL connections from the configured connection string. Connections are
/// created lazily: nothing is opened until a consumer calls <see cref="Create" />.
/// </summary>
public sealed class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly SqlOptions _options;

    public PostgresConnectionFactory(IOptions<SqlOptions> options) => _options = options.Value;

    public DbConnection Create() => new NpgsqlConnection(_options.ConnectionString);
}
