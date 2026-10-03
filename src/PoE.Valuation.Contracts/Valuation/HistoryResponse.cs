namespace PoE.Valuation.Contracts.Valuation;

/// <summary>
/// A valuation history series (GET /api/history/{itemId}?range=24h|7d|30d).
/// </summary>
/// <param name="ItemId">The item id the series is for.</param>
/// <param name="ReferenceItemId">The reference item id the rates are expressed in.</param>
/// <param name="League">League name (e.g. <c>Standard</c>).</param>
/// <param name="Range">The requested range as accepted by the endpoint (24h, 7d, 30d).</param>
/// <param name="FromUtc">Inclusive start of the window, UTC.</param>
/// <param name="Count">Number of points in <see cref="Points"/>.</param>
/// <param name="Points">Hourly points, newest first.</param>
/// <param name="Cached">True when the response was served from the Redis history cache.</param>
public sealed record HistoryResponse(
    string ItemId,
    string ReferenceItemId,
    string League,
    string Range,
    DateTimeOffset FromUtc,
    int Count,
    IReadOnlyList<HistoryPointDto> Points,
    bool Cached);
