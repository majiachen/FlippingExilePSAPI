namespace PoE.Valuation.Contracts.Valuation;

/// <summary>One stash item as exposed by GET /api/stash (a subset of the PoE stash API item).</summary>
/// <param name="ItemId">The PoE item id (base id plus modifier suffixes).</param>
/// <param name="League">League name the stash was fetched for (e.g. <c>Standard</c>).</param>
/// <param name="Name">Item name; equals <see cref="BaseType"/> for named uniques.</param>
/// <param name="BaseType">Base type of the item.</param>
/// <param name="FrameType">PoE frame type code (0 normal, 1 magic, 2 rare, 3 unique, ...).</param>
/// <param name="ItemLevel">Item level, when published.</param>
/// <param name="StackSize">Stack size for stackable items, when published.</param>
/// <param name="GemLevel">Gem level for skill/essence gems, when published.</param>
/// <param name="GemQuality">Gem quality, when published.</param>
/// <param name="Corrupted">Whether the item is corrupted.</param>
/// <param name="Verified">Whether the item is verified for trading.</param>
/// <param name="Note">Player note, when set.</param>
public sealed record StashItemDto(
    string ItemId,
    string League,
    string Name,
    string BaseType,
    int FrameType,
    int ItemLevel,
    int? StackSize,
    int? GemLevel,
    int? GemQuality,
    bool Corrupted,
    bool Verified,
    string? Note);
