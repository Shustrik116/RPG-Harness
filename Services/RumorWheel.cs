using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>Служебные данные анимации честного колеса купленных слухов.</summary>
public static class RumorWheel
{
    public const string MetaPrefix = "[[rumor-wheel ";
    private static string[] Faces => new[] { Title("minor"), Title("medium"), Title("long") };

    public sealed record Spin(int D100, string Tier, List<string> Reel);
    public sealed record Meta(string Source, List<Spin> Spins);

    public static Spin MakeSpin(int d100, string tier)
    {
        var reel = Enumerable.Range(0, 9).Select(_ => Faces[Random.Shared.Next(Faces.Length)]).ToList();
        reel.Add(Title(tier));
        return new Spin(d100, tier, reel);
    }

    public static string BuildMeta(string source, IEnumerable<Spin> spins) =>
        MetaPrefix + JsonSerializer.Serialize(new Meta(source, spins.ToList())) + "]]";

    public static Meta? ParseMeta(string? result)
    {
        if (string.IsNullOrEmpty(result) || !result.StartsWith(MetaPrefix, StringComparison.Ordinal)) return null;
        var end = result.IndexOf("]]\n", StringComparison.Ordinal);
        if (end < 0 && result.EndsWith("]]", StringComparison.Ordinal)) end = result.Length - 2;
        if (end < 0) return null;
        try { return JsonSerializer.Deserialize<Meta>(result[MetaPrefix.Length..end]); }
        catch { return null; }
    }

    public static string Title(string tier) => tier switch
    {
        "medium" => Genre.PickT("средний квест", "средний заказ", "среднее дело", "medium quest", "medium gig", "medium job"),
        "long" => Genre.PickT("длинный квест", "длинный заказ", "крупное дело", "long quest", "long gig", "big job"),
        _ => Lang.T("малая зацепка", "small lead"),
    };
}
