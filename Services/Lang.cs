namespace RPG_Harness.Services;

/// <summary>
/// Язык интерфейса и игры: русский или английский. От языка зависят подписи интерфейса, промпт мастера,
/// описания и результаты инструментов, каталоги (названия вещей, портретов, навыков) и шаблоны файлов кампании —
/// мастер получает всё на одном языке и сам пишет на нём.
///
/// Язык задаётся в настройках (HarnessSettings.Language) и действует на всё приложение сразу — как и сеттинг
/// (<see cref="Genre"/>): одно приложение на одного игрока. Пустое значение в настройках значит «ещё не выбран»:
/// при первом запуске интерфейс спрашивает язык. Смена языка перезагружает страницу целиком.
///
/// Строки в коде пишутся парой <c>Lang.T("по-русски", "in English")</c>. Русский — исходный язык проекта:
/// он же запасной для всего, что ещё не переведено.
/// </summary>
public static class Lang
{
    public const string Ru = "ru";
    public const string En = "en";

    /// <summary>Поддерживаемые языки: ид, самоназвание и подпись — в порядке показа игроку.</summary>
    public static readonly (string Id, string Native, string Hint)[] All =
    {
        (Ru, "Русский", "Интерфейс, мастер и тексты игры — на русском."),
        (En, "English", "Interface, game master and game texts in English."),
    };

    private static volatile string _current = Ru;

    /// <summary>Текущий язык приложения. Меняет его только SettingsService.</summary>
    public static string Current
    {
        get => _current;
        set => _current = Normalize(value);
    }

    public static bool IsEn => _current == En;

    public static bool IsRu => _current == Ru;

    /// <summary>Язык из настроек или сохранения: всё неизвестное и пустое — русский.</summary>
    public static string Normalize(string? id) => (id ?? "").Trim().ToLowerInvariant() switch
    {
        En or "english" => En,
        _ => Ru,
    };

    /// <summary>Язык выбран игроком (в настройках есть известное значение).</summary>
    public static bool IsKnown(string? id) => (id ?? "").Trim().ToLowerInvariant() is Ru or En or "english";

    /// <summary>Строка на текущем языке.</summary>
    public static string T(string ru, string en) => _current == En ? en : ru;

    /// <summary>Выбор любого значения по языку (таблицы, массивы вариантов).</summary>
    public static TValue Pick<TValue>(TValue ru, TValue en) => _current == En ? en : ru;

    /// <summary>Код культуры для атрибута lang и форматирования.</summary>
    public static string HtmlLang => _current;

    /// <summary>
    /// Число с существительным: по-русски три формы (1 раунд, 2 раунда, 5 раундов), по-английски две (1 round, 2 rounds).
    /// </summary>
    public static string Plural(long n, string one, string few, string many, string enOne, string enMany)
    {
        if (_current == En)
        {
            return Math.Abs(n) == 1 ? enOne : enMany;
        }

        var a = Math.Abs(n) % 100;
        var b = a % 10;
        return a is >= 11 and <= 14 ? many : b == 1 ? one : b is >= 2 and <= 4 ? few : many;
    }
}
