using System.Text.Json;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.UnitTests;

public class MarketDigestDeserializationTests
{
    // Mirrors the options used by PoeCurrencyExchangeClient.FetchDigestAsync.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CurrencyExchangeResponse_Binds_LiveApiPayload_WithArrayMarketPair()
    {
        const string payload = """
            {
              "next_change_id": 1790478000,
              "markets": [
                {
                  "league": "Standard",
                  "market_id": "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex|Metadata/Items/Currency/CurrencyRerollRare",
                  "market_pair": [
                    "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex",
                    "Metadata/Items/Currency/CurrencyRerollRare"
                  ],
                  "volume_traded": { "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex": 4, "Metadata/Items/Currency/CurrencyRerollRare": 245 },
                  "lowest_stock": { "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex": 1311, "Metadata/Items/Currency/CurrencyRerollRare": 4060 },
                  "highest_stock": { "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex": 1311, "Metadata/Items/Currency/CurrencyRerollRare": 5260 },
                  "lowest_ratio": { "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex": 1, "Metadata/Items/Currency/CurrencyRerollRare": 65 },
                  "highest_ratio": { "Metadata/Items/MapFragments/CurrencyUberBossKeyCortex": 1, "Metadata/Items/Currency/CurrencyRerollRare": 60 }
                }
              ]
            }
            """;

        var response = JsonSerializer.Deserialize<CurrencyExchangeResponse>(payload, JsonOptions);

        Assert.NotNull(response);
        Assert.Equal(1790478000L, response.NextChangeId);
        var market = Assert.Single(response.Markets);
        Assert.Equal("Standard", market.League);
        Assert.Equal("Metadata/Items/MapFragments/CurrencyUberBossKeyCortex|Metadata/Items/Currency/CurrencyRerollRare", market.MarketId);
        Assert.Equal("Metadata/Items/MapFragments/CurrencyUberBossKeyCortex", market.MarketPair.CurrencyA);
        Assert.Equal("Metadata/Items/Currency/CurrencyRerollRare", market.MarketPair.CurrencyB);
        Assert.Equal(245L, market.VolumeTraded["Metadata/Items/Currency/CurrencyRerollRare"]);
        Assert.Equal(5260L, market.HighestStock["Metadata/Items/Currency/CurrencyRerollRare"]);
    }

    [Fact]
    public void MarketDigest_Serializes_MarketPair_AsArray()
    {
        var market = new MarketDigest(
            "Standard", "a|b",
            new MarketPair("a", "b"),
            new Dictionary<string, long> { ["a"] = 1, ["b"] = 2 },
            new Dictionary<string, long> { ["a"] = 3, ["b"] = 4 },
            new Dictionary<string, long> { ["a"] = 5, ["b"] = 6 },
            new Dictionary<string, double> { ["a"] = 7.0, ["b"] = 8.0 },
            new Dictionary<string, double> { ["a"] = 9.0, ["b"] = 10.0 });

        var json = JsonSerializer.Serialize(market, JsonOptions);

        Assert.Contains("\"market_pair\":[\"a\",\"b\"]", json);
    }

    [Fact]
    public void MarketPair_Throws_WhenPayloadIsObject()
    {
        const string payload = """
            {
              "league": "Standard",
              "market_id": "a|b",
              "market_pair": { "ItemA": "a", "ItemB": "b" },
              "volume_traded": {},
              "lowest_stock": {},
              "highest_stock": {},
              "lowest_ratio": {},
              "highest_ratio": {}
            }
            """;

        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MarketDigest>(payload, JsonOptions));
        Assert.Contains("market_pair", ex.Message);
    }
}
