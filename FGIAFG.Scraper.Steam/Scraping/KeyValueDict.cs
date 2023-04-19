using SteamKit2;

namespace FGIAFG.Scraper.Steam.Scraping;

internal class KeyValueDict
{
    public string Name { get; }
    public string Value { get; }
    public Dictionary<string, KeyValueDict> Children { get; }

    public KeyValueDict this[string key] => Children[key];

    public KeyValueDict(KeyValue kv)
    {
        Name = kv.Name;
        Value = kv.Value;

        Dictionary<string, KeyValueDict> children = new Dictionary<string, KeyValueDict>();

        foreach (KeyValue keyValue in kv.Children)
        {
            children.Add(keyValue.Name, new KeyValueDict(keyValue));
        }

        Children = children;
    }

    public bool Contains(string key)
    {
        return Children.ContainsKey(key);
    }
}
