namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class AccountAndStashMap
{
    public String accountName;
    public List<Stash> Stashes;
    
    public override string ToString()
    {
        var stashIds = Stashes?.Select(s => s.Id).ToList() ?? new List<string>();
        return $"Account: {accountName}, Stash IDs: [{string.Join(", ", stashIds)}]";
    }

}