namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class LeagueMarketData
{
    public uint next_change_id { get; set; }
    
    public List<LeagueMarket> markets { get; set; } = new List<LeagueMarket>();
}

public class LeagueMarket
{
    public string league { get; set; } = string.Empty;
    
    public string market_id { get; set; } = string.Empty;
    
    public Dictionary<string, uint> volume_traded { get; set; } = new Dictionary<string, uint>();
    
    public Dictionary<string, uint> lowest_stock { get; set; } = new Dictionary<string, uint>();
    
    public Dictionary<string, uint> highest_stock { get; set; } = new Dictionary<string, uint>();
    
    public Dictionary<string, uint> lowest_ratio { get; set; } = new Dictionary<string, uint>();
    
    public Dictionary<string, uint> highest_ratio { get; set; } = new Dictionary<string, uint>();
}