namespace PoE.Valuation.Contracts.Valuation;

/// <summary>One hourly data point of a valuation history series (GET /api/history/{itemId}).</summary>
/// <param name="SnapshotHourUtc">Start of the digest hour, UTC.</param>
/// <param name="LowestRate">Lowest observed rate this hour (units of the reference per 1 item), when available.</param>
/// <param name="HighestRate">Highest observed rate this hour (units of the reference per 1 item), when available.</param>
/// <param name="VolumeItem">Units of the item traded in the hour, when published.</param>
/// <param name="VolumeReference">Units of the reference item traded in the hour, when published.</param>
public sealed record HistoryPointDto(
    DateTimeOffset SnapshotHourUtc,
    double? LowestRate,
    double? HighestRate,
    long? VolumeItem,
    long? VolumeReference);
