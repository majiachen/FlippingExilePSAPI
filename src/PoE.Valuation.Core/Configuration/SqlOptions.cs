namespace PoE.Valuation.Core.Configuration;

/// <summary>PostgreSQL settings for hourly currency-exchange storage.</summary>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";
    public const string ConnectionStringKey = "ConnectionString";

    /// <summary>PostgreSQL connection string, e.g. <c>Host=192.168.0.10;Port=5432;Database=poevaluation;Username=postgres;Password=postgres</c>.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
