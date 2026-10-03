using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>
/// Английские названия каталогов — оверлей wwwroot/sprites/i18n/en.json поверх items.json, cyber-items.json,
/// modern-items.json и portraits.json (по id). Оверлей лежит отдельно, чтобы скрипты сборки атласов
/// (tools/sprites/*.py), перезаписывающие сами каталоги, не стирали перевод. Новый предмет или портрет без
/// записи здесь показывается по-русски — допишите его сюда.
/// </summary>
public static class CatalogEn
{
    private static readonly object Gate = new();
    private static Dictionary<string, string> _items = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _portraits = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _kinds = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static void Load(string webRoot)
    {
        lock (Gate)
        {
            try
            {
                var path = Path.Combine(webRoot, "sprites", "i18n", "en.json");
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                _items = Read(doc.RootElement, "items");
                _portraits = Read(doc.RootElement, "portraits");
                _kinds = Read(doc.RootElement, "kinds");
            }
            catch
            {
                // без оверлея английский интерфейс показывает русские названия каталогов
            }

            _loaded = true;
        }
    }

    private static Dictionary<string, string> Read(JsonElement root, string key)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(key, out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in obj.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String) map[p.Name] = p.Value.GetString() ?? "";
            }
        }

        return map;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        Load(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        if (_items.Count == 0) Load(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
    }

    public static string? Item(string? id)
    {
        EnsureLoaded();
        return id is not null && _items.TryGetValue(id, out var v) && v.Length > 0 ? v : null;
    }

    public static string? Portrait(string? id)
    {
        EnsureLoaded();
        return id is not null && _portraits.TryGetValue(id, out var v) && v.Length > 0 ? v : null;
    }

    public static string? Kind(string? kind)
    {
        EnsureLoaded();
        return kind is not null && _kinds.TryGetValue(kind, out var v) && v.Length > 0 ? v : null;
    }

    /// <summary>Основы слов английского названия для угадывания иконки или облика по имени («Iron golem» → iron, golem).</summary>
    public static List<string> Keywords(string? name, string? id = null)
    {
        var words = (name ?? "").ToLowerInvariant()
            .Split(new[] { ' ', '-', '"', '\'', '(', ')', ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && w is not ("the" or "and" or "with" or "for" or "of"))
            .Select(w => w.Length > 5 && w.EndsWith('s') ? w[..^1] : w);
        var fromId = (id ?? "").ToLowerInvariant().Split('_', '-').Where(w => w.Length >= 3 && w is not ("cy" or "md"));
        return words.Concat(fromId).Distinct().ToList();
    }
}
