namespace RPG_Harness.Services;

/// <summary>
/// Справочник тем оформления. Тема одновременно задаёт сеттинг игры (<see cref="Genre"/>):
/// «Фэнтези» — гримуар и мечи, «Киберпанк» — неоновый город, импланты и стволы,
/// «Современность» — наши дни: автоматы, машины, криминал и полиция.
/// Палитра темы — набор токенов --rh-* в wwwroot/css/themes.css, к которому страница подключается
/// по data-theme на &lt;html&gt;. Идентификатор хранится в настройках (HarnessSettings.Theme);
/// снятая тема прошлых версий «Терминал» нормализуется в киберпанк.
/// </summary>
public static class UiThemes
{
    /// <summary>Базовая тема «Фэнтези» — палитра из :root файла app.css.</summary>
    public const string Fantasy = Genre.Fantasy;

    public const string Cyberpunk = Genre.Cyberpunk;

    public const string Modern = Genre.Modern;

    /// <summary>Ид темы → (название, подпись в настройках). Порядок — как показывать игроку.</summary>
    public static (string Id, string Title, string Hint)[] All => Lang.IsEn ? AllEn : AllRu;

    private static readonly (string Id, string Title, string Hint)[] AllRu =
    {
        (Fantasy, "Фэнтези",
            "Гримуар и пергамент, латунный акцент. Мечи, магия, гоблины и драконы."),
        (Cyberpunk, "Киберпанк",
            "Ночной мегаполис, кислотный неон и HUD. Импланты, стволы, корпорации и нетраннеры."),
        (Modern, "Современность",
            "Наши дни: от реализма до городского фэнтези. Оружие, машины, полиция, тайная магия и сверхъестественные угрозы — по выбору."),
    };

    private static readonly (string Id, string Title, string Hint)[] AllEn =
    {
        (Fantasy, "Fantasy",
            "Grimoire and parchment, a brass accent. Swords, magic, goblins and dragons."),
        (Cyberpunk, "Cyberpunk",
            "A megacity at night, acid neon and a HUD. Implants, guns, corporations and netrunners."),
        (Modern, "Modern",
            "The present day: from realism to urban fantasy. Guns, cars, police, secret magic and supernatural threats — your choice."),
    };

    /// <summary>Тема из настроек: снятая «Терминал» ближе к киберпанку, неизвестное — фэнтези.</summary>
    public static string Normalize(string? id) => id switch
    {
        Cyberpunk or "terminal" => Cyberpunk,
        Modern => Modern,
        _ => Fantasy,
    };

    /// <summary>Название темы для подписи в интерфейсе.</summary>
    public static string TitleOf(string? id) => Genre.Title(Normalize(id));
}
