using System.Text.Json;

namespace RPG_Harness.Services;

/// <summary>
/// Хранилище диалогов. Каждый чат — отдельный JSON-файл в {WorkDirectory}/chats.
/// Если директория не выбрана, чаты живут только в памяти.
/// </summary>
public sealed class ChatStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SettingsService _settings;
    private readonly object _lock = new();
    private List<ChatSession>? _cache;

    public ChatStore(SettingsService settings)
    {
        _settings = settings;
        _settings.Saved += OnSettingsSaved;
    }

    /// <summary>Вызывается при изменении состава/порядка чатов.</summary>
    public event Action? Changed;

    /// <summary>Полный путь папки с чатами или null, если директория не выбрана.</summary>
    public string? StorageDirectory
    {
        get
        {
            var dir = _settings.Current.WorkDirectory;
            return string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir, "chats");
        }
    }

    /// <summary>Последняя ошибка записи на диск (null — ошибок не было).</summary>
    public string? LastError { get; private set; }

    public IReadOnlyList<ChatSession> Sessions
    {
        get
        {
            lock (_lock)
            {
                EnsureLoadedNoLock();
                return _cache!.OrderByDescending(c => c.UpdatedAt).ToList();
            }
        }
    }

    public ChatSession? Get(string id)
    {
        lock (_lock)
        {
            EnsureLoadedNoLock();
            return _cache!.FirstOrDefault(c => c.Id == id);
        }
    }

    public ChatSession Create()
    {
        var session = new ChatSession();
        lock (_lock)
        {
            EnsureLoadedNoLock();
            _cache!.Add(session);
        }

        Persist(session);
        NotifyChanged();
        return session;
    }

    public void Delete(string id)
    {
        lock (_lock)
        {
            EnsureLoadedNoLock();
            _cache!.RemoveAll(c => c.Id == id);
        }

        var dir = StorageDirectory;
        if (dir is not null)
        {
            try
            {
                var file = Path.Combine(dir, id + ".json");
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        NotifyChanged();
    }

    /// <summary>Сериализует чат на диск (если директория выбрана).</summary>
    public void Persist(ChatSession session)
    {
        session.UpdatedAt = DateTime.UtcNow;

        var dir = StorageDirectory;
        if (dir is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, session.Id + ".json"), JsonSerializer.Serialize(session, JsonOptions));
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    /// <summary>Сбросить кэш и перечитать чаты с диска (вызывается при смене директории).</summary>
    public void Reload()
    {
        lock (_lock)
        {
            _cache = null;
        }

        NotifyChanged();
    }

    public void NotifyChanged() => Changed?.Invoke();

    private void OnSettingsSaved(HarnessSettings _) => Reload();

    private void EnsureLoadedNoLock()
    {
        if (_cache is not null)
        {
            return;
        }

        _cache = new List<ChatSession>();

        var dir = StorageDirectory;
        if (dir is null || !Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var session = JsonSerializer.Deserialize<ChatSession>(File.ReadAllText(file), JsonOptions);
                if (session is not null)
                {
                    _cache.Add(session);
                }
            }
            catch
            {
                // пропускаем повреждённые файлы
            }
        }
    }
}