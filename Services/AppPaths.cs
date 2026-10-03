namespace RPG_Harness.Services;

/// <summary>Каталоги приложения: конфиг всегда лежит рядом с проектом.</summary>
public sealed class AppPaths
{
    public AppPaths(string contentRoot)
    {
        ContentRoot = contentRoot;
        ConfigDir = Path.Combine(contentRoot, ".harness");
    }

    public string ContentRoot { get; }

    /// <summary>Служебная папка конфигурации (внутри рабочего каталога приложения).</summary>
    public string ConfigDir { get; }

    public string SettingsFile => Path.Combine(ConfigDir, "settings.json");
}