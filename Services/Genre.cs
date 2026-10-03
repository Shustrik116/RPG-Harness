namespace RPG_Harness.Services;

/// <summary>
/// Сеттинг игры: «Фэнтези», «Киберпанк» или «Современность». Это не только оформление — от сеттинга зависят
/// промпт мастера, описания инструментов, каталоги предметов, портреты, иконки навыков, торговцы, добыча,
/// колёса дороги и слухов.
///
/// Сеттинг задаётся темой в настройках (UiThemes) и действует на всё приложение сразу. Кампания запоминает
/// свой сеттинг при создании (RpgState.GenreId) и открывается только в нём: так фэнтезийная кампания
/// не получит чужой промпт посреди игры. Глобальное значение держим статическим — в Blazor Server
/// одно приложение на одного игрока, а каталоги и инструменты читают его из десятков мест.
/// </summary>
public static class Genre
{
    public const string Fantasy = "fantasy";
    public const string Cyberpunk = "cyberpunk";
    public const string Modern = "modern";

    private static volatile string _current = Fantasy;

    /// <summary>Текущий сеттинг приложения (из темы настроек). Меняет его только SettingsService.</summary>
    public static string Current
    {
        get => _current;
        set => _current = Normalize(value);
    }

    public static bool IsCyber => _current == Cyberpunk;

    public static bool IsModern => _current == Modern;

    public static bool IsFantasy => _current == Fantasy;

    /// <summary>Сеттинг из темы/сохранения: всё неизвестное и пустое (старые кампании) — фэнтези.</summary>
    public static string Normalize(string? id) => id switch
    {
        Cyberpunk => Cyberpunk,
        Modern => Modern,
        _ => Fantasy,
    };

    /// <summary>Сеттинг кампании: в старых сохранениях поля нет — это фэнтези.</summary>
    public static string Of(RpgState state) => Normalize(state.GenreId);

    public static string Title(string? id) => Normalize(id) switch
    {
        Cyberpunk => Lang.T("Киберпанк", "Cyberpunk"),
        Modern => Lang.T("Современность", "Modern"),
        _ => Lang.T("Фэнтези", "Fantasy"),
    };

    /// <summary>Выбор по текущему сеттингу — для подписей интерфейса, результатов инструментов и таблиц.</summary>
    public static T Pick<T>(T fantasy, T cyber, T modern) => _current switch
    {
        Cyberpunk => cyber,
        Modern => modern,
        _ => fantasy,
    };

    /// <summary>Выбор по сеттингу и языку: сначала три русских варианта, затем три английских в том же порядке.</summary>
    public static string PickT(string fantasy, string cyber, string modern, string fantasyEn, string cyberEn, string modernEn) =>
        Lang.IsEn ? Pick(fantasyEn, cyberEn, modernEn) : Pick(fantasy, cyber, modern);

    // ===== Термины сеттинга =====

    /// <summary>Деньги полностью: «золото» / «эдди» / «деньги».</summary>
    public static string Money => Lang.IsEn ? Pick("gold", "eddies", "money") : Pick("золото", "эдди", "деньги");

    /// <summary>Деньги после числа в тексте: «50 золота» / «50 эдди» / «50 $».</summary>
    public static string MoneyGen => Lang.IsEn ? Pick("gold", "eddies", "$") : Pick("золота", "эдди", "$");

    /// <summary>Значок денег после числа: «12 з» / «12 €$» / «12 $».</summary>
    public static string Coin => Pick(Lang.T("з", "g"), "€$", "$");

    /// <summary>Ресурс навыков: «мана» / «ОЗУ» / «концентрация».</summary>
    public static string Mana => Lang.IsEn ? Pick("mana", "RAM", "focus") : Pick("мана", "ОЗУ", "концентрация");

    /// <summary>Короткая метка ресурса на полосах: MP / RAM / КНЦ.</summary>
    public static string ManaShort => Pick("MP", "RAM", Lang.T("КНЦ", "FOC"));

    /// <summary>Ресурс навыков в родительном падеже: «маны» / «ОЗУ» / «концентрации».</summary>
    public static string ManaGen => Lang.IsEn ? Pick("mana", "RAM", "focus") : Pick("маны", "ОЗУ", "концентрации");

    // ===== Перевод текстов механики в другие сеттинги =====

    /// <summary>
    /// Фразы и слова механики, общие для всех сеттингов, которые в киберпанке и современности называются иначе.
    /// Сначала длинные фразы (контекст), затем отдельные слова — строго по границам слов, чтобы «обмана» не стала «обОЗУ».
    /// </summary>
    private static readonly (string From, string To)[] CyberPhrases =
    {
        ("атака гоблина", "атака ганкера"), ("урон топором", "урон из дробовика"),
        ("Колесо дороги", "Колесо улиц"), ("колесо дороги", "колесо улиц"), ("колеса дороги", "колеса улиц"),
        ("пределов поселения", "пределов района"),
        ("кузнец, трактирщица, капитан стражи", "риппердок, барменша, сержант полиции"),
        ("волк → wolf", "киберпсих → cyberpsycho"),
        ("гильдии приключенцев", "сети фиксеров"), ("гильдия приключенцев", "сеть фиксеров"),
        ("отделение гильдии", "фиксера с доской контрактов"), ("отделения гильдии", "фиксера"), ("отделении гильдии", "у фиксера"),
        ("Репутация гильдии", "Уличная репутация"), ("репутацию гильдии", "уличную репутацию"), ("репутации гильдии", "уличной репутации"),
        ("таверна / храм / рынок / гильдия / кузница / стража / замок", "бар / клиника / рынок / фиксер / оружейная / участок / корпоративная башня"),
        ("в трактире или лавке", "в баре, у информатора или в даркнете"),
        ("зелья, стрелы, еда", "стимы, патроны, еда"),
        ("город, лес, данж, дорога", "район, промзона, башня, Пустоши"),
        ("Крупный город", "Крупный район или город"),
        ("звери дают трофеи (клыки, шкуры, мясо), гуманоиды — мелочь из карманов, трофейное оружие, зелья и золото, драконы — чешую и клад, нежить — кости и старые реликвии",
            "люди дают мелочь из карманов, трофейные стволы, стимы и эдди, киборги — снятые импланты, дроны и мехи — платы и аккумуляторы, программы — фрагменты кода, мутанты — образцы тканей"),
        ("Тип существа: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity",
            "Тип противника: humanoid (человек), cyborg (киборг), android (андроид), drone (дрон), mech (мех, боевой робот, турель), program (программа или ИИ), mutant (мутант), beast (зверь, кибер-зверь)"),
        ("урон в ближнем бою, урон издалека, магия, защита, лечение, поддержка, контроль", "урон в ближнем бою, урон издалека, взлом, защита, медицина, поддержка, контроль"),
        ("«храмовник», «пиромантка»", "«соло», «нетраннерша»"),
        ("«1.5К огнём + поджог»", "«1.5К ЭМИ + короткое замыкание»"),
        ("«Гоблин 1», «Гоблин 2»", "«Ганкер 1», «Ганкер 2»"), ("«Гоблин 1»", "«Ганкер 1»"), ("«Гоблин-разбойник 1»", "«Ганкер 1»"), ("Гоблин-разбойник 1", "Ганкер 1"),
        ("«Сильвана»", "«Лира»"), ("гильдия F", "репутация F"), ("гильдия E", "репутация E"), ("гильдия D", "репутация D"),
        ("гильдия C", "репутация C"), ("гильдия B", "репутация B"), ("гильдия A", "репутация A"), ("гильдия S", "репутация S"),
        ("рубящий, колющий, огонь, яд…", "кинетический, режущий, ЭМИ, кибератака, токсин…"),
        ("Текущая мана", "Текущее ОЗУ"), ("Максимальная мана", "Максимальное ОЗУ"), ("Стоимость маны", "Стоимость ОЗУ"),
        ("Легендарная", "Культовая"), ("легендарная", "культовая"), ("легендарное", "культовое"), ("легендарную", "культовую"),
        ("легендарной", "культовой"), ("легендарных", "культовых"),
    };

    private static readonly (string From, string To)[] CyberWordList =
    {
        ("золото", "эдди"), ("Золото", "Эдди"), ("золота", "эдди"), ("золоте", "эдди"), ("золотом", "эдди"), ("золоту", "эдди"),
        ("мана", "ОЗУ"), ("маны", "ОЗУ"), ("ману", "ОЗУ"), ("мане", "ОЗУ"), ("маной", "ОЗУ"), ("Мана", "ОЗУ"),
        ("зелье", "стим"), ("зелья", "стимы"), ("зелий", "стимов"), ("зельями", "стимами"), ("Зелья", "Стимы"),
        ("гильдия", "фиксер"), ("гильдии", "фиксера"), ("гильдию", "фиксера"), ("гильдией", "фиксером"),
        ("монеты", "эдди"), ("монет", "эдди"), ("монетами", "эдди"),
        ("дробящий", "ударный"), ("дробящего", "ударного"), ("рубящий", "режущий"), ("рубящего", "режущего"),
        ("заклинание", "программа"), ("заклинания", "программы"),
        ("квест", "заказ"), ("квесты", "заказы"), ("квеста", "заказа"), ("квестов", "заказов"),
        ("поселение", "район"), ("поселения", "района"), ("поселении", "районе"), ("поселений", "районов"),
        ("трактир", "бар"), ("трактире", "баре"), ("трактирщик", "бармен"),
        ("данж", "локация"), ("данжа", "локации"), ("данже", "локации"),
        ("добротная", "модифицированная"), ("добротный", "модифицированный"), ("добротное", "модифицированное"), ("добротные", "модифицированные"),
        ("эпическая", "прототип"), ("эпический", "прототип"), ("эпическое", "прототип"), ("эпическую", "прототип"), ("эпической", "прототипа"),
    };

    /// <summary>Современность: узнаваемый мир, в котором сверхъестественное может отсутствовать, скрываться или быть обыденным.</summary>
    private static readonly (string From, string To)[] ModernPhrases =
    {
        ("атака гоблина", "атака бандита"), ("урон топором", "урон из дробовика"),
        ("пределов поселения", "пределов города"),
        ("кузнец, трактирщица, капитан стражи", "автомеханик, барменша, участковый"),
        ("волк → wolf", "сторожевой пёс → guard_dog"),
        ("гильдии приключенцев", "сети посредников"), ("гильдия приключенцев", "сеть посредников"),
        ("отделение гильдии", "посредника с заказами"), ("отделения гильдии", "посредника"), ("отделении гильдии", "у посредника"),
        ("Репутация гильдии", "Репутация в узких кругах"), ("репутацию гильдии", "репутацию в узких кругах"), ("репутации гильдии", "репутации в узких кругах"),
        ("таверна / храм / рынок / гильдия / кузница / стража / замок", "бар / больница / рынок / посредник / автосервис / полиция / офис"),
        ("в трактире или лавке", "в баре, у информатора или через знакомых"),
        ("зелья, стрелы, еда", "лекарства, патроны, еда"),
        ("город, лес, данж, дорога", "район, лес, здание, трасса"),
        ("Крупный город", "Крупный город или район"),
        ("звери дают трофеи (клыки, шкуры, мясо), гуманоиды — мелочь из карманов, трофейное оружие, зелья и золото, драконы — чешую и клад, нежить — кости и старые реликвии",
            "люди дают мелочь из карманов, трофейное оружие, лекарства и деньги, машины и техника — запчасти и топливо, звери — шкуры и мясо"),
        ("Тип существа: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity",
            "Тип противника: humanoid (человек), beast (зверь), vehicle (транспорт), drone (дрон), undead (нежить), construct (оживлённый предмет), demon (демон), aberration (аномалия), elemental (стихийная сущность), monstrosity (чудовище)"),
        ("урон в ближнем бою, урон издалека, магия, защита, лечение, поддержка, контроль", "урон в ближнем бою, урон издалека, магия или техника и взлом, защита, медицина, поддержка, контроль"),
        ("«храмовник», «пиромантка»", "«городской маг», «полевая медсестра»"),
        ("«1.5К огнём + поджог»", "«1.5К огнестрельным + кровотечение»"),
        ("«Гоблин 1», «Гоблин 2»", "«Бандит 1», «Бандит 2»"), ("«Гоблин 1»", "«Бандит 1»"), ("«Гоблин-разбойник 1»", "«Бандит 1»"), ("Гоблин-разбойник 1", "Бандит 1"),
        ("«Сильвана»", "«Марина»"), ("гильдия F", "репутация F"), ("гильдия E", "репутация E"), ("гильдия D", "репутация D"),
        ("гильдия C", "репутация C"), ("гильдия B", "репутация B"), ("гильдия A", "репутация A"), ("гильдия S", "репутация S"),
        ("рубящий, колющий, огонь, яд…", "огнестрельный, режущий, ударный, огонь, взрыв…"),
        ("Текущая мана", "Текущая концентрация"), ("Максимальная мана", "Максимальная концентрация"), ("Стоимость маны", "Стоимость концентрации"),
    };

    private static readonly (string From, string To)[] ModernWordList =
    {
        ("золото", "деньги"), ("Золото", "Деньги"), ("золота", "денег"), ("золоте", "деньгах"), ("золотом", "деньгами"), ("золоту", "деньгам"),
        ("мана", "концентрация"), ("маны", "концентрации"), ("ману", "концентрацию"), ("мане", "концентрации"), ("маной", "концентрацией"), ("Мана", "Концентрация"),
        ("зелье", "препарат"), ("зелья", "препараты"), ("зелий", "препаратов"), ("зельями", "препаратами"), ("Зелья", "Препараты"),
        ("гильдия", "посредник"), ("гильдии", "посредника"), ("гильдию", "посредника"), ("гильдией", "посредником"),
        ("монеты", "деньги"), ("монет", "денег"), ("монетами", "деньгами"),
        ("дробящий", "ударный"), ("дробящего", "ударного"), ("рубящий", "режущий"), ("рубящего", "режущего"),
        ("заклинание", "заклинание или спецприём"), ("заклинания", "заклинания или спецприёма"),
        ("квест", "задание"), ("квесты", "задания"), ("квеста", "задания"), ("квестов", "заданий"),
        ("поселение", "город"), ("поселения", "города"), ("поселении", "городе"), ("поселений", "городов"),
        ("трактир", "бар"), ("трактире", "баре"), ("трактирщик", "бармен"),
        ("данж", "объект"), ("данжа", "объекта"), ("данже", "объекте"),
        ("добротная", "улучшенная"), ("добротный", "улучшенный"), ("добротное", "улучшенное"), ("добротные", "улучшенные"),
        ("эпическая", "эксклюзивная"), ("эпический", "эксклюзивный"), ("эпическое", "эксклюзивное"), ("эпическую", "эксклюзивную"), ("эпической", "эксклюзивной"),
    };

    private static (System.Text.RegularExpressions.Regex Rx, string To)[] Compile((string From, string To)[] words) =>
        words.Select(w => (new System.Text.RegularExpressions.Regex($@"(?<![\p{{L}}]){System.Text.RegularExpressions.Regex.Escape(w.From)}(?![\p{{L}}])"), w.To)).ToArray();

    private static readonly (System.Text.RegularExpressions.Regex Rx, string To)[] CyberWords = Compile(CyberWordList);
    private static readonly (System.Text.RegularExpressions.Regex Rx, string To)[] ModernWords = Compile(ModernWordList);

    /// <summary>«12 з» → «12 €$» / «12 $».</summary>
    private static readonly System.Text.RegularExpressions.Regex CoinRx = new(@"(?<=\d)\s?з(?![\p{L}])");

    /// <summary>
    /// Переводит текст механики в термины текущего сеттинга (киберпанк: эдди, ОЗУ, фиксер; современность: деньги,
    /// концентрация, посредник). В фэнтези возвращает как есть.
    /// </summary>
    public static string Localize(string? text)
    {
        if (IsFantasy || string.IsNullOrEmpty(text))
        {
            return text ?? "";
        }

        if (Lang.IsEn)
        {
            return LocalizeEn(text);
        }

        var (phrases, words) = IsCyber ? (CyberPhrases, CyberWords) : (ModernPhrases, ModernWords);
        var s = text;
        foreach (var (from, to) in phrases)
        {
            s = s.Replace(from, to, StringComparison.Ordinal);
        }

        foreach (var (rx, to) in words)
        {
            s = rx.Replace(s, to);
        }

        return CoinRx.Replace(s, " " + Coin);
    }

    // ===== Английские тексты механики =====
    // Английские слова совпадают с ключами параметров (mana, quest, dungeon, uncommon, potions…), поэтому здесь
    // меняются только фразы и слова, которые ключами не бывают; граница слова исключает «_» и «=» (mana_max, update_guild_job).

    private static readonly (string From, string To)[] CyberPhrasesEn =
    {
        ("goblin attack", "ganger attack"), ("axe damage", "shotgun damage"),
        ("The road wheel", "The street wheel"), ("Road wheel", "Street wheel"), ("road wheel", "street wheel"),
        ("beyond the settlement", "beyond the district"),
        ("blacksmith, innkeeper, captain of the guard", "ripperdoc, bartender, police sergeant"),
        ("wolf → wolf", "cyberpsycho → cyberpsycho"),
        ("a branch of the adventurers' guild", "a fixer with a contract board"),
        ("adventurers' guild", "fixer network"), ("Adventurers' Guild", "Fixer network"),
        ("Guild reputation", "Street reputation"), ("guild reputation", "street reputation"),
        ("tavern / temple / market / guild / forge / guard / castle / other", "bar / clinic / market / fixer / armory / precinct / corporate tower / other"),
        ("in a tavern or a shop", "in a bar, from an informant or on the darknet"),
        ("potions, arrows, food", "stims, ammo, food"),
        ("a town, a forest, a dungeon, a road", "a district, an industrial zone, a tower, the Wastelands"),
        ("beasts give trophies (fangs, hides, meat), humanoids — pocket change, captured weapons, potions and gold, dragons — scales and hoards, undead — bones and old relics",
            "people give pocket change, captured guns, stims and eddies, cyborgs — ripped-out implants, drones and mechs — boards and batteries, programs — code fragments, mutants — tissue samples"),
        ("Creature type: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity",
            "Adversary type: humanoid (a human), cyborg, android, drone, mech (a mech, combat robot, turret), program (a program or AI), mutant, beast (an animal, cyber-beast)"),
        ("melee damage, ranged damage, magic, defense, healing, support, control", "melee damage, ranged damage, hacking, defense, medicine, support, control"),
        ("\"templar\", \"pyromancer\"", "\"solo\", \"netrunner\""),
        ("\"1.5K fire + burning\"", "\"1.5K EMP + short circuit\""),
        ("\"Goblin 1\", \"Goblin 2\"", "\"Ganger 1\", \"Ganger 2\""), ("\"Goblin 1\"", "\"Ganger 1\""), ("\"Goblin Raider 1\"", "\"Ganger 1\""), ("Goblin Raider 1", "Ganger 1"),
        ("\"Sylvana\"", "\"Lyra\""),
        ("slashing, piercing, fire, poison…", "kinetic, slashing, EMP, cyberattack, toxin…"),
        ("Current mana", "Current RAM"), ("Maximum mana", "Maximum RAM"), ("Mana cost", "RAM cost"), ("mana cost", "RAM cost"),
        ("HP and mana", "HP and RAM"), ("HP, mana", "HP, RAM"), ("spending mana", "spending RAM"),
        ("Gold for completion", "Eddies for completion"), ("Price per unit in gold", "Price per unit in eddies"), ("The total price in gold", "The total price in eddies"),
    };

    private static readonly (string From, string To)[] ModernPhrasesEn =
    {
        ("goblin attack", "bandit attack"), ("axe damage", "shotgun damage"),
        ("beyond the settlement", "beyond the city"),
        ("blacksmith, innkeeper, captain of the guard", "car mechanic, bartender, local cop"),
        ("wolf → wolf", "guard dog → guard_dog"),
        ("a branch of the adventurers' guild", "a middleman with jobs"),
        ("adventurers' guild", "middleman network"), ("Adventurers' Guild", "Middleman network"),
        ("Guild reputation", "Reputation in certain circles"), ("guild reputation", "reputation in certain circles"),
        ("tavern / temple / market / guild / forge / guard / castle / other", "bar / hospital / market / middleman / car shop / police / office / other"),
        ("in a tavern or a shop", "in a bar, from an informant or through acquaintances"),
        ("potions, arrows, food", "medicine, ammo, food"),
        ("a town, a forest, a dungeon, a road", "a district, a forest, a building, a highway"),
        ("beasts give trophies (fangs, hides, meat), humanoids — pocket change, captured weapons, potions and gold, dragons — scales and hoards, undead — bones and old relics",
            "people give pocket change, captured weapons, medicine and money, vehicles and machinery — parts and fuel, animals — hides and meat"),
        ("Creature type: humanoid, beast, undead, construct, dragon, demon, aberration, elemental, plant, ooze, giant, celestial, monstrosity",
            "Adversary type: humanoid (a human), beast (an animal), vehicle, drone, undead, construct (an animated object), demon, aberration (an anomaly), elemental, monstrosity"),
        ("melee damage, ranged damage, magic, defense, healing, support, control", "melee damage, ranged damage, magic or tech and hacking, defense, medicine, support, control"),
        ("\"templar\", \"pyromancer\"", "\"city mage\", \"field nurse\""),
        ("\"1.5K fire + burning\"", "\"1.5K ballistic + bleeding\""),
        ("\"Goblin 1\", \"Goblin 2\"", "\"Bandit 1\", \"Bandit 2\""), ("\"Goblin 1\"", "\"Bandit 1\""), ("\"Goblin Raider 1\"", "\"Bandit 1\""), ("Goblin Raider 1", "Bandit 1"),
        ("\"Sylvana\"", "\"Marina\""),
        ("slashing, piercing, fire, poison…", "ballistic, slashing, blunt, fire, explosive…"),
        ("Current mana", "Current focus"), ("Maximum mana", "Maximum focus"), ("Mana cost", "Focus cost"), ("mana cost", "focus cost"),
        ("HP and mana", "HP and focus"), ("HP, mana", "HP, focus"), ("spending mana", "spending focus"),
        ("Gold for completion", "Money for completion"), ("Price per unit in gold", "Price per unit in money"), ("The total price in gold", "The total price in money"),
    };

    private static (System.Text.RegularExpressions.Regex Rx, string To)[] CompileEn((string From, string To)[] words) =>
        words.Select(w => (new System.Text.RegularExpressions.Regex($@"(?<![\p{{L}}_=]){System.Text.RegularExpressions.Regex.Escape(w.From)}(?![\p{{L}}_])"), w.To)).ToArray();

    private static readonly (System.Text.RegularExpressions.Regex Rx, string To)[] CyberWordsEn = CompileEn(new[]
    {
        ("gold", "eddies"), ("Gold", "Eddies"), ("guilds", "fixers"), ("guild", "fixer"), ("Guild", "Fixer"),
        ("settlements", "districts"), ("settlement", "district"), ("taverns", "bars"), ("tavern", "bar"), ("innkeeper", "bartender"),
        ("spells", "programs"), ("spell", "program"),
    });

    private static readonly (System.Text.RegularExpressions.Regex Rx, string To)[] ModernWordsEn = CompileEn(new[]
    {
        ("gold", "money"), ("Gold", "Money"), ("guilds", "middlemen"), ("guild", "middleman"), ("Guild", "Middleman"),
        ("settlements", "towns"), ("settlement", "town"), ("taverns", "bars"), ("tavern", "bar"), ("innkeeper", "bartender"),
    });

    /// <summary>«(−3 mana)», «12 mana» — число перед словом «mana» значит ресурс, а не ключ параметра.</summary>
    private static readonly System.Text.RegularExpressions.Regex ManaAfterNumberRx = new(@"(?<=\d\s)mana(?![\p{L}_])");

    private static string LocalizeEn(string text)
    {
        var (phrases, words) = IsCyber ? (CyberPhrasesEn, CyberWordsEn) : (ModernPhrasesEn, ModernWordsEn);
        var s = text;
        foreach (var (from, to) in phrases)
        {
            s = s.Replace(from, to, StringComparison.Ordinal);
        }

        foreach (var (rx, to) in words)
        {
            s = rx.Replace(s, to);
        }

        return ManaAfterNumberRx.Replace(s, Mana);
    }
}
