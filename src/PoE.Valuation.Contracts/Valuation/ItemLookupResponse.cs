namespace PoE.Valuation.Contracts.Valuation;

/// <summary>
/// Resolves a human item name (or a trade alias, or a metadata path) to the item ids the valuation
/// endpoints accept — the bridge between a stash item's <c>name</c>/<c>base_type</c> and the
/// currency-exchange data (GET /api/items/lookup).
/// </summary>
/// <param name="Query">The query exactly as supplied.</param>
/// <param name="CatalogUpdatedAtUtc">When the catalogue was last refreshed, UTC; null when it has never been refreshed (no matches are then possible).</param>
/// <param name="Count">Number of matches in <see cref="Matches"/>.</param>
/// <param name="Matches">Ranked matches, strongest first.</param>
public sealed record ItemLookupResponse(
    string Query,
    DateTimeOffset? CatalogUpdatedAtUtc,
    int Count,
    IReadOnlyList<ItemMatchDto> Matches);

/// <summary>Result of POST /api/items/catalog/refresh (the manual catalogue refresh).</summary>
/// <param name="RefreshedCount">Catalogue rows written by this refresh; 0 when another instance held the refresh lock.</param>
/// <param name="TotalCount">Catalogue rows now stored.</param>
/// <param name="CatalogUpdatedAtUtc">When the catalogue was last refreshed, UTC.</param>
public sealed record ItemCatalogRefreshResponse(
    int RefreshedCount,
    int TotalCount,
    DateTimeOffset? CatalogUpdatedAtUtc);

