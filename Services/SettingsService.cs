using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>Загрузка/сохранение настроек в .harness/settings.json.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AppPaths _paths;
    private readonly object _lock = new();

    public SettingsService(AppPaths paths)
    {
        _paths = paths;
        var (settings, migrated) = Load();
        Current = settings;
        Genre.Current = settings.ActiveGenre;
        Lang.Current = settings.ActiveLanguage;

        if (migrated)
        {
            // Зафиксировать миграцию на диске: иначе файл останется расходиться с кодом до первого сохранения.
            Save(Current);
        }
    }

    public HarnessSettings Current { get; private set; }

    /// <summary>Событие после применения (сохранения) настроек.</summary>
    public event Action<HarnessSettings>? Saved;

    public HarnessSettings Clone() => Current.Clone();

    private (HarnessSettings Settings, bool Migrated) Load()
    {
        try
        {
            if (File.Exists(_paths.SettingsFile))
            {
                var json = File.ReadAllText(_paths.SettingsFile);
                var settings = JsonSerializer.Deserialize<HarnessSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    // Удалённый флаг toolsEnabled больше ни на что не влияет и исчезает при миграции файла.
                    using var document = JsonDocument.Parse(json);
                    var migrated = document.RootElement.TryGetProperty("toolsEnabled", out _);

                    // Файл старого формата (флага нет): считаем промпт базовым, если он пуст, это заглушка
                    // ранней версии, прежний базовый или текущий базовый. Решение фиксируем в файле.
                    if (settings.SystemPromptIsDefault is null)
                    {
                        settings.SystemPromptIsDefault = GmPrompt.IsUnmodifiedDefault(settings.SystemPrompt);
                        migrated = true;
                    }

                    // Базовый промпт всегда актуализируем; правку игрока оставляем как есть.
                    if (settings.SystemPromptIsDefault == true && !GmPrompt.IsCurrentDefault(settings.SystemPrompt))
                    {
                        settings.SystemPrompt = GmPrompt.Default;
                        migrated = true;
                    }

                    // Киберпанковый промпт: пустой или нетронутый базовый держим актуальным.
                    if (string.IsNullOrWhiteSpace(settings.SystemPromptCyber) ||
                        settings.SystemPromptCyberIsDefault && !GmPrompt.IsCurrentCyberDefault(settings.SystemPromptCyber))
                    {
                        settings.SystemPromptCyber = GmPrompt.CyberDefault;
                        settings.SystemPromptCyberIsDefault = true;
                        migrated = true;
                    }

                    if (string.IsNullOrWhiteSpace(settings.SystemPromptModern) ||
                        settings.SystemPromptModernIsDefault && !GmPrompt.IsCurrentModernDefault(settings.SystemPromptModern))
                    {
                        settings.SystemPromptModern = GmPrompt.ModernDefault;
                        settings.SystemPromptModernIsDefault = true;
                        migrated = true;
                    }

                    // Английские промпты: пустой или нетронутый базовый держим актуальным, как и русские.
                    migrated |= KeepEnglishDefaults(settings);

                    // Снятая тема «Терминал» — в ближайшую из оставшихся.
                    var theme = UiThemes.Normalize(settings.Theme);
                    if (theme != settings.Theme)
                    {
                        settings.Theme = theme;
                        migrated = true;
                    }

                    // Старый нетронутый набор температур заменяем новым стандартом; пользовательские профили сохраняем.
                    var old = new Dictionary<string, double>
                    {
                        [RequestKinds.World] = .95, [RequestKinds.Story] = .85, [RequestKinds.Combat] = .4,
                        [RequestKinds.Trade] = .5, [RequestKinds.Ooc] = .35,
                    };
                    var legacyPreset = old.All(kv => settings.Profiles.TryGetValue(kv.Key, out var p) && p.Temperature == kv.Value);
                    var recentLegacyPreset = settings.Profiles.TryGetValue(RequestKinds.World, out var pw) && pw.Temperature == 1 &&
                                             settings.Profiles.TryGetValue(RequestKinds.Story, out var ps) && ps.Temperature == 1 &&
                                             settings.Profiles.TryGetValue(RequestKinds.Combat, out var pc) && pc.Temperature == .6 &&
                                             settings.Profiles.TryGetValue(RequestKinds.Trade, out var pt) && pt.Temperature == .5 &&
                                             settings.Profiles.TryGetValue(RequestKinds.Ooc, out var po) && po.Temperature == .5;
                    if (legacyPreset || recentLegacyPreset)
                    {
                        settings.Profiles = RequestKinds.DefaultProfiles();
                        migrated = true;
                    }

                    return (settings, migrated);
                }
            }
        }
        catch
        {
            // повреждённый файл настроек — используем значения по умолчанию
        }

        return (new HarnessSettings { SystemPromptIsDefault = true }, false);
    }

    /// <summary>
    /// Английские промпты: пустой (файл настроек до появления языков) или нетронутый базовый заменяем актуальным
    /// базовым из кода; правку игрока не трогаем. Возвращает true, если что-то изменилось.
    /// </summary>
    private static bool KeepEnglishDefaults(HarnessSettings settings)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(settings.SystemPromptEn) ||
            settings.SystemPromptEnIsDefault && !GmPrompt.IsCurrentDefaultEn(settings.SystemPromptEn))
        {
            settings.SystemPromptEn = GmPrompt.DefaultEn;
            settings.SystemPromptEnIsDefault = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.SystemPromptCyberEn) ||
            settings.SystemPromptCyberEnIsDefault && !GmPrompt.IsCurrentCyberDefaultEn(settings.SystemPromptCyberEn))
        {
            settings.SystemPromptCyberEn = GmPrompt.CyberDefaultEn;
            settings.SystemPromptCyberEnIsDefault = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.SystemPromptModernEn) ||
            settings.SystemPromptModernEnIsDefault && !GmPrompt.IsCurrentModernDefaultEn(settings.SystemPromptModernEn))
        {
            settings.SystemPromptModernEn = GmPrompt.ModernDefaultEn;
            settings.SystemPromptModernEnIsDefault = true;
            changed = true;
        }

        return changed;
    }

    /// <summary>Применяет настройки и пытается записать их на диск.</summary>
    /// <returns>null при успехе, либо текст ошибки записи (настройки применены всё равно).</returns>
    public string? Save(HarnessSettings settings)
    {
        string? writeError = null;

        lock (_lock)
        {
            // Промпт, совпавший с базовым, снова берём под автообновление; всё прочее — правка игрока.
            settings.SystemPromptIsDefault = GmPrompt.IsCurrentDefault(settings.SystemPrompt);
            if (settings.SystemPromptIsDefault == true)
            {
                settings.SystemPrompt = GmPrompt.Default;
            }

            settings.SystemPromptCyberIsDefault = GmPrompt.IsCurrentCyberDefault(settings.SystemPromptCyber);
            if (settings.SystemPromptCyberIsDefault)
            {
                settings.SystemPromptCyber = GmPrompt.CyberDefault;
            }

            settings.SystemPromptModernIsDefault = GmPrompt.IsCurrentModernDefault(settings.SystemPromptModern);
            if (settings.SystemPromptModernIsDefault)
            {
                settings.SystemPromptModern = GmPrompt.ModernDefault;
            }

            settings.SystemPromptEnIsDefault = GmPrompt.IsCurrentDefaultEn(settings.SystemPromptEn);
            settings.SystemPromptCyberEnIsDefault = GmPrompt.IsCurrentCyberDefaultEn(settings.SystemPromptCyberEn);
            settings.SystemPromptModernEnIsDefault = GmPrompt.IsCurrentModernDefaultEn(settings.SystemPromptModernEn);
            KeepEnglishDefaults(settings);

            settings.Theme = UiThemes.Normalize(settings.Theme);
            settings.Language = Lang.IsKnown(settings.Language) ? Lang.Normalize(settings.Language) : "";

            try
            {
                Directory.CreateDirectory(_paths.ConfigDir);
                File.WriteAllText(_paths.SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch (Exception ex)
            {
                writeError = ex.Message;
            }

            Current = settings.Clone();
            Genre.Current = Current.ActiveGenre;
            Lang.Current = Current.ActiveLanguage;
        }

        Saved?.Invoke(Current);
        return writeError;
    }
}
