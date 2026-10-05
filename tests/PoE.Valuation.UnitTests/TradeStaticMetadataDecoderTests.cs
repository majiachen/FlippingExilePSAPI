using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.UnitTests;

/// <summary>
/// The catalogue endpoint publishes no metadata field; the path is inside the icon URL's base64url
/// payload. These cases use image strings captured from the live
/// <c>GET /api/trade/data/static</c> response, so they assert the real shape rather than a guess.
/// </summary>
public class TradeStaticMetadataDecoderTests
{
    [Theory]
    // Chaos Orb, as published by the live catalogue.
    [InlineData("/gen/image/WzI1LDE0LHsiZiI6IjJESXRlbXMvQ3VycmVuY3kvQ3VycmVuY3lSZXJvbGxSYXJlIiwic2NhbGUiOjF9XQ/46a2347805/CurrencyRerollRare.png",
        "Metadata/Items/Currency/CurrencyRerollRare")]
    // Divine Orb.
    [InlineData("/gen/image/WzI1LDE0LHsiZiI6IjJESXRlbXMvQ3VycmVuY3kvQ3VycmVuY3lNb2RWYWx1ZXMiLCJzY2FsZSI6MX1d/ec48896769/CurrencyModValues.png",
        "Metadata/Items/Currency/CurrencyModValues")]
    // Gemcutter's Prism.
    [InlineData("/gen/image/WzI1LDE0LHsiZiI6IjJESXRlbXMvQ3VycmVuY3kvQ3VycmVuY3lHZW1RdWFsaXR5Iiwic2NhbGUiOjF9XQ/dbe9678a28/CurrencyGemQuality.png",
        "Metadata/Items/Currency/CurrencyGemQuality")]
    public void TryDecodeMetadata_DecodesLiveCatalogueIconUrls(string image, string expectedMetadata)
    {
        Assert.True(TradeStaticMetadataDecoder.TryDecodeMetadata(image, out var metadata));
        Assert.Equal(expectedMetadata, metadata);
    }

    [Fact]
    public void TryDecodeMetadata_AcceptsAlreadyQualifiedAssetPath()
    {
        var image = Payload("Metadata/Items/Currency/CurrencyRerollRare");

        Assert.True(TradeStaticMetadataDecoder.TryDecodeMetadata(image, out var metadata));
        Assert.Equal("Metadata/Items/Currency/CurrencyRerollRare", metadata);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/gen/image/not!base64!at!all/6308fc8ca2/CurrencyRerollMagic.png")]
    [InlineData("/gen/image/AAAAAAAAAAAAAAAA/6308fc8ca2/CurrencyRerollMagic.png")] // valid base64, no "f" member
    [InlineData("/gen/image/WzI1LDE0LHsiZmlsZSI6Im5vIGZlciJ9XQ/6308fc8ca2/X.png")] // JSON, but no "f" member
    public void TryDecodeMetadata_RejectsUsablelessImages(string? image)
    {
        Assert.False(TradeStaticMetadataDecoder.TryDecodeMetadata(image, out var metadata));
        Assert.Equal(string.Empty, metadata);
    }

    [Fact]
    public void TryDecodeMetadata_RejectsFileOutsideItemAssetTree()
    {
        var image = Payload("2DArt/Currency/CurrencyRerollRare");

        Assert.False(TradeStaticMetadataDecoder.TryDecodeMetadata(image, out var metadata));
        Assert.Equal(string.Empty, metadata);
    }

    [Fact]
    public void TryDecodeMetadata_IgnoresIconHashSegmentThatHappensToBeBase64Shaped()
    {
        // The hash segment is a valid base64 length; only the segment that decodes to a JSON object
        // with an "f" member may win.
        var image = "/gen/image/" + PayloadSegment("2DItems/Currency/CurrencyRerollRare") + "/AAAAAAAAAAAAAAAA/CurrencyRerollRare.png";

        Assert.True(TradeStaticMetadataDecoder.TryDecodeMetadata(image, out var metadata));
        Assert.Equal("Metadata/Items/Currency/CurrencyRerollRare", metadata);
    }

    /// <summary>Builds an icon URL in the catalogue's shape for an arbitrary asset path.</summary>
    private static string Payload(string assetPath) =>
        "/gen/image/" + PayloadSegment(assetPath) + "/6308fc8ca2/Icon.png";

    private static string PayloadSegment(string assetPath)
    {
        var json = $"[25,14,{{\"f\":\"{assetPath}\",\"scale\":1}}]";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
