using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.LeaguePOCO;

public class League
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Realm { get; set; }
    public string Url { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string Description { get; set; }
    public Category Category { get; set; }
    public DateTime? RegisterAt { get; set; }
    public bool DelveEvent { get; set; }
    public List<Rule> Rules { get; set; }
    public string Goal { get; set; }
    public bool TimedEvent { get; set; }
}

public class Category
{
    public string Id { get; set; }
    public bool? Current { get; set; }
}

public class Rule
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}