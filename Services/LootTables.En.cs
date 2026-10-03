namespace RPG_Harness.Services;

/// <summary>
/// Английские тексты таблиц добычи. Таблицы записаны по-русски (вероятности и цены в одном месте),
/// а названия и заметки переводятся при выдаче вещи: так перевод не размножает таблицы.
/// </summary>
public static partial class LootTables
{
    /// <summary>Текст таблицы на языке интерфейса (неизвестная строка остаётся как есть).</summary>
    private static string Tr(string ru) => Lang.IsEn && En.TryGetValue(ru, out var en) ? en : ru;

    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        // Трофеи современности
        ["Шкура ({0})"] = "Hide ({0})", ["Мясо ({0})"] = "Meat ({0})", ["Клыки ({0})"] = "Fangs ({0})",
        ["Скупщики и охотники берут."] = "Fences and hunters will take it.", ["Съедобно, если приготовить."] = "Edible if cooked.",
        ["Трофей на память или на продажу."] = "A trophy to keep or to sell.",
        ["Запчасти ({0})"] = "Parts ({0})", ["Аккумулятор ({0})"] = "Battery ({0})", ["Канистра бензина"] = "Gas canister",
        ["В автосервисе возьмут."] = "A car shop will take them.", ["Полная, если повезло."] = "Full, if you're lucky.",
        ["Электроника ({0})"] = "Electronics ({0})", ["Платы и камера — в магазин электроники."] = "Boards and a camera — for an electronics store.",

        // Трофеи киберпанка
        ["Нейрочип ({0})"] = "Neurochip ({0})", ["Снят с киборга; риппердоки берут охотно."] = "Pulled from a cyborg; ripperdocs buy gladly.",
        ["Сервоприводы ({0})"] = "Servos ({0})", ["Детали протезов."] = "Prosthetic parts.", ["Хромированные детали"] = "Chrome parts",
        ["Процессорный модуль ({0})"] = "Processor module ({0})", ["Мозги андроида."] = "An android's brains.", ["Синтекожа"] = "Synthskin",
        ["Силовой элемент ({0})"] = "Power cell ({0})", ["Ещё держит заряд."] = "Still holds a charge.", ["Оптика ({0})"] = "Optics ({0})",
        ["Проводка ({0})"] = "Wiring ({0})", ["Бронепластины ({0})"] = "Armor plates ({0})", ["Тяжёлая броня, лом на продажу."] = "Heavy armor, scrap for sale.",
        ["Сервопривод ({0})"] = "Servo ({0})", ["Силовое ядро ({0})"] = "Power core ({0})",
        ["Ценная деталь — техники отдадут хорошие эдди."] = "A valuable part — techies will pay good eddies.",
        ["Фрагмент кода ({0})"] = "Code fragment ({0})", ["Нетраннеры платят за такое."] = "Netrunners pay for this.",
        ["Ключ шифрования"] = "Encryption key", ["Может открыть чужой сервер."] = "Might open someone else's server.",
        ["Образец тканей ({0})"] = "Tissue sample ({0})", ["Биолаборатории покупают."] = "Biolabs buy these.", ["Мутировавшая ткань"] = "Mutated tissue",
        ["Кибер-имплант ({0})"] = "Cyber-implant ({0})", ["Вживлён в зверя."] = "Implanted in the beast.", ["Образец ({0})"] = "Sample ({0})",

        // Трофеи фэнтези
        ["Трофей охотника; алхимики и кожевники берут охотно."] = "A hunter's trophy; alchemists and tanners buy gladly.",
        ["Выделывается в кожу или мех."] = "Can be tanned into leather or fur.",
        ["Когти ({0})"] = "Claws ({0})", ["Трофей и алхимический реагент."] = "A trophy and an alchemical reagent.",
        ["Прочная шкура чудовища."] = "The tough hide of a monster.", ["Череп ({0})"] = "Skull ({0})", ["Внушительный трофей."] = "An imposing trophy.",
        ["Чешуя ({0})"] = "Scales ({0})", ["Материал для драконьего доспеха."] = "Material for dragon armor.",
        ["Драконий клык ({0})"] = "Dragon fang ({0})", ["Ценнейший трофей."] = "A most precious trophy.",
        ["Рога ({0})"] = "Horns ({0})", ["Трофей, достойный легенды."] = "A trophy worthy of legend.",
        ["Кости ({0})"] = "Bones ({0})", ["Некроманты платят за такие."] = "Necromancers pay for these.",
        ["Могильная пыль"] = "Grave dust", ["Реагент для тёмных зелий."] = "A reagent for dark potions.",
        ["Слиток ({0})"] = "Ingot ({0})", ["Металл конструкта."] = "Construct metal.", ["Шестерни и пружины"] = "Gears and springs",
        ["Для изобретателей."] = "For inventors.", ["Кристалл-ядро ({0})"] = "Core crystal ({0})", ["Источник силы конструкта."] = "The construct's power source.",
        ["Пахнут серой."] = "They smell of sulfur.", ["Осколок адского камня"] = "Shard of hellstone", ["Тёплый на ощупь."] = "Warm to the touch.",
        ["Руна бездны"] = "Rune of the abyss", ["Опасный артефакт иного мира."] = "A dangerous artifact from another world.",
        ["Эссенция ({0})"] = "Essence ({0})", ["Сгусток стихии: реагент для зачарований."] = "A clot of the element: a reagent for enchanting.",
        ["Самоцвет стихии"] = "Elemental gem", ["Едкая субстанция для алхимии."] = "A caustic substance for alchemy.",
        ["Травы ({0})"] = "Herbs ({0})", ["Целебное сырьё."] = "Healing raw material.", ["Споры ({0})"] = "Spores ({0})", ["Алхимический реагент."] = "An alchemical reagent.",
        ["Осколок бездны"] = "Shard of the abyss", ["Шепчет, если прислушаться."] = "It whispers if you listen.",
        ["Глаз ({0})"] = "Eye ({0})", ["Мутная сфера, всё ещё следит."] = "A cloudy orb, still watching.",
        ["Печать бездны"] = "Seal of the abyss", ["Знак нечеловеческого разума."] = "The sign of an inhuman mind.",
        ["Меха ({0})"] = "Furs ({0})", ["Трофейный череп"] = "Trophy skull", ["Перья ({0})"] = "Feathers ({0})",
        ["Светятся в темноте."] = "They glow in the dark.", ["Осколок небесного света"] = "Shard of heavenly light",

        // Мелочь современности
        ["Пачка сигарет"] = "Pack of cigarettes", ["Зажигалка"] = "Lighter", ["Одноразовый телефон"] = "Burner phone",
        ["Пара номеров в контактах — может пригодиться."] = "A couple of numbers in the contacts — might come in handy.",
        ["Чужой смартфон"] = "Someone's smartphone", ["Заблокирован. Если взломать — переписка."] = "Locked. Crack it and you get the messages.",
        ["Ключи от машины"] = "Car keys", ["Где-то стоит машина, которая к ним подходит."] = "Somewhere there's a car they fit.",
        ["Связка ключей"] = "Bunch of keys", ["Фотография"] = "Photograph", ["Лотерейный билет"] = "Lottery ticket",
        ["Стереть защитный слой?"] = "Scratch it off?", ["Флешка"] = "Flash drive", ["Мало ли что на ней."] = "Who knows what's on it.",
        ["Золотая цепочка"] = "Golden chain", ["Сдать в ломбард."] = "Pawn it.",
        ["Фонарик"] = "Flashlight", ["Моток верёвки"] = "Coil of rope", ["Мультитул"] = "Multitool", ["Рация"] = "Walkie-talkie",
        ["Бинокль"] = "Binoculars", ["Ювелирные украшения"] = "Jewelry", ["Коробка сигар"] = "Box of cigars",
        ["Папка с документами"] = "Folder of documents", ["Чьи-то договоры и подписи."] = "Someone's contracts and signatures.",

        // Мелочь киберпанка
        ["Мятая пачка сигарет"] = "Crumpled pack of cigarettes", ["Игральные кости"] = "Dice", ["Чья-то удача, теперь твоя."] = "Someone's luck, now yours.",
        ["Банка газировки"] = "Can of soda", ["Энергетик"] = "Energy drink", ["Ключ-карта"] = "Keycard", ["Неизвестно, от какой двери."] = "No telling which door it opens.",
        ["Разбитый холофон"] = "Cracked holophone", ["Можно сдать на запчасти или покопаться в памяти."] = "Sell it for parts or dig through its memory.",
        ["Старое фото"] = "Old photo", ["Шард с записями"] = "Shard with notes", ["Личные заметки, контакты, может — компромат."] = "Personal notes, contacts, maybe dirt on someone.",
        ["Голофигурка"] = "Holofigure", ["Коллекционная безделушка."] = "A collectible trinket.", ["Золотые часы"] = "Golden watch",
        ["Кабель"] = "Cable", ["Фонарь"] = "Lantern", ["Сканер"] = "Scanner", ["Цифровой бинокль"] = "Digital binoculars",
        ["Кристалл памяти"] = "Memory crystal", ["Виниловая пластинка"] = "Vinyl record", ["Настоящий винил — редкость."] = "Real vinyl is a rarity.",

        // Мелочь фэнтези
        ["Костяные игральные кости"] = "Bone dice", ["Фляга дешёвого эля"] = "Flask of cheap ale", ["Вяленое мясо"] = "Jerky",
        ["Чёрствый хлеб"] = "Stale bread", ["Факел"] = "Torch", ["Ржавый ключ"] = "Rusty key", ["Неизвестно, от чего."] = "No telling what it opens.",
        ["Медное зеркальце"] = "Small copper mirror", ["Табакерка"] = "Snuffbox", ["Резной оберег"] = "Carved charm",
        ["Деревенский талисман на удачу."] = "A village good-luck talisman.", ["Помятая серебряная фляга"] = "Dented silver flask",
        ["Кусочек янтаря"] = "Piece of amber", ["Истлевшая шкатулка"] = "Rotted casket", ["Потускневшее зеркальце"] = "Tarnished small mirror",
        ["Погребальная статуэтка"] = "Funerary figurine", ["Старинная работа; коллекционеры ценят."] = "Antique work; collectors value it.",
        ["Бледный камень из глазницы"] = "Pale stone from an eye socket", ["Серебряное зеркальце"] = "Small silver mirror",
        ["Резная шкатулка"] = "Carved casket", ["Статуэтка"] = "Figurine", ["Компас"] = "Compass",

        // Свойства вещей
        ["не ржавеет и не тупится"] = "never rusts or dulls", ["светится в темноте"] = "glows in the dark",
        ["тёплый: холод не страшен"] = "warm: the cold is no threat", ["лёгкий: не мешает скрытности"] = "light: doesn't hinder stealth",
        ["преимущество на Выживание в дикой местности"] = "advantage on Survival in the wilds", ["не промокает и не горит"] = "never gets wet and never burns",
        ["удар поджигает: +{d} огнём"] = "the strike ignites: +{d} fire", ["удар леденит: +{d} холодом"] = "the strike chills: +{d} cold",
        ["удар разрядом: +{d} электричеством"] = "a shocking strike: +{d} lightning", ["яд на клинке: +{d} ядом"] = "poison on the blade: +{d} poison",
        ["против нежити: +{d} светом"] = "against undead: +{d} radiant", ["критическое попадание на 19–20"] = "critical hit on 19–20",
        ["вампиризм: лечит 1d6 при попадании"] = "vampirism: heals 1d6 on hit",
        ["раз в бой — оглушающий удар (спасбросок ТЕЛ)"] = "once per combat — a stunning strike (CON save)",
        ["сопротивление огню"] = "fire resistance", ["сопротивление холоду"] = "cold resistance", ["сопротивление яду"] = "poison resistance",
        ["сопротивление некротике"] = "necrotic resistance", ["преимущество на спасброски от страха"] = "advantage on saves against fear",
        ["раз в бой — щит на 10% макс. ХП"] = "once per combat — a shield for 10% of max HP", ["шаги бесшумны"] = "silent footsteps",
        ["иммунитет к оглушению"] = "immunity to stun",
        ["видит в темноте на 20 шагов"] = "darkvision out to 20 paces",
        ["раз в день — заклинание Щит (+5 КБ до конца раунда)"] = "once per day — the Shield spell (+5 AC until the end of the round)",
        ["+1 ко всем спасброскам"] = "+1 to all saving throws", ["раз в бой — восстановить 1К маны"] = "once per combat — restore 1K mana",
        ["сопротивление психическому урону"] = "psychic damage resistance", ["раз в день — благословение на группу"] = "once per day — a blessing on the party",
        ["невидимость ночью, пока не атакуешь"] = "invisibility at night until you attack",
        ["раз в день — вернуться из смерти с 1 ХП"] = "once per day — return from death with 1 HP",
        ["полёт на минуту раз в день"] = "flight for a minute once per day", ["призыв духа-волка раз в бой"] = "summon a spirit wolf once per combat",
        ["понимает любой язык"] = "understands any language", ["раз в день — остановить время на 1 раунд"] = "once per day — stop time for 1 round",

        // Расходники
        ["Бинты и обезболивающее"] = "Bandages and painkillers", ["Аптечка"] = "First aid kit", ["Армейская аптечка"] = "Army first aid kit",
        ["Набор военного медика"] = "Combat medic kit", ["Таблетки кофеина"] = "Caffeine pills", ["Армейский стимулятор"] = "Army stimulant",
        ["Армейский стимулятор (усиленный)"] = "Army stimulant (reinforced)", ["Противоядие"] = "Antidote", ["Снимает отравление."] = "Cures poisoning.",
        ["Светошумовая граната"] = "Flashbang grenade",
        ["Ослепление и оглушение на 1 раунд в зоне (спасбросок ТЕЛ DC 13)."] = "Blinds and stuns for 1 round in an area (CON save DC 13).",
        ["Медстим"] = "Medstim", ["Медстим+"] = "Medstim+", ["Военный медстим"] = "Military medstim", ["Медстим «Травма»"] = "\"Trauma\" medstim",
        ["Стим ОЗУ"] = "RAM stim", ["Стим ОЗУ+"] = "RAM stim+", ["Военный стим ОЗУ"] = "Military RAM stim", ["Стим ОЗУ «Овердрайв»"] = "\"Overdrive\" RAM stim",
        ["Антитоксин"] = "Antitoxin", ["Снимает отравление токсином."] = "Cures toxin poisoning.", ["Инъекция бронегеля"] = "Armor-gel injection",
        ["Малое зелье лечения"] = "Minor healing potion", ["Зелье лечения"] = "Healing potion", ["Большое зелье лечения"] = "Greater healing potion",
        ["Высшее зелье лечения"] = "Supreme healing potion", ["Малое зелье маны"] = "Minor mana potion", ["Зелье маны"] = "Mana potion",
        ["Большое зелье маны"] = "Greater mana potion", ["Высшее зелье маны"] = "Supreme mana potion", ["Свиток защиты"] = "Scroll of protection",
        ["Трофейное: побывало в бою, но служит."] = "Captured: it has seen battle, but it still serves.",
    };
}
