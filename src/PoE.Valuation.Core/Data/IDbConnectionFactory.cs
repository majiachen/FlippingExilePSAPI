using System.Data.Common;

namespace PoE.Valuation.Core.Data;

/// <summary>
/// Creates database connections for the valuation store. Implemented by Infrastructure;
/// consumers receive unopened connections and open them themselves.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates a new, unopened connection.</summary>
    DbConnection Create();
}
