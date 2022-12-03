using Newtonsoft.Json.Linq;

namespace StableHordeProxy.Api.Model;

public class Model : IComparable<Model>
{
    public Model(string name, JToken? value)
    {
        Name = name;
        if (value == null) return;

        //Try to get each property from the JToken
        Description = value?["description"]?.ToString() ?? "";
        Trigger = value?["trigger"]?.Select(x => x.ToString()).ToArray() ?? Array.Empty<string>();
        Showcases = value?["showcases"]?.Select(x => x.ToString()).ToArray() ?? Array.Empty<string>();
        Style = value?["style"]?.ToString() ?? "";
        Nsfw = value?["nsfw"]?.Value<bool>() ?? false;
    }

    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Trigger { get; set; }
    public string[] Showcases { get; set; }
    public string Style { get; set; }
    public bool Nsfw { get; set; }

    public int AvailableWorkers { get; set; } = 0;

    public int SortIndex { get; set; } = 0;

    public int CompareTo(Model? other)
    {
        if (other == null) return 1;
        return string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    public void Update(Model modelValue)
    {
        Description = modelValue.Description;
        Trigger = modelValue.Trigger;
        Showcases = modelValue.Showcases;
        Style = modelValue.Style;
        Nsfw = modelValue.Nsfw;
    }

    public static bool operator ==(Model a, Model b)
    {
        return a.Name == b.Name && a.Description == b.Description && a.Trigger == b.Trigger && a.Showcases == b.Showcases && a.Style == b.Style && a.Nsfw == b.Nsfw;
    }

    public static bool operator !=(Model a, Model b)
    {
        return !(a == b);
    }
}