using System.Text.RegularExpressions;

namespace RPG_Harness.Services;

/// <summary>
/// Типы урона в данных механики (таблицы оружия, каталоги киберпанка и современности, урон врагов по умолчанию)
/// записаны по-русски. На английском интерфейсе они переводятся при выдаче — «1d8 рубящий» → «1d8 slashing»,
/// чтобы мастер и игрок видели одни и те же слова, что и в английском промпте.
/// </summary>
public static class DamageWords
{
    /// <summary>Русские формы → английское слово. Длинные формы раньше коротких, совпадение по границам слов.</summary>
    private static readonly (string Ru, string En)[] Words =
    {
        ("рубящего", "slashing"), ("рубящий", "slashing"), ("рубящим", "slashing"),
        ("колющего", "piercing"), ("колющий", "piercing"), ("колющим", "piercing"),
        ("дробящего", "bludgeoning"), ("дробящий", "bludgeoning"), ("дробящим", "bludgeoning"),
        ("режущего", "slashing"), ("режущий", "slashing"), ("режущим", "slashing"),
        ("ударного", "blunt"), ("ударный", "blunt"), ("ударным", "blunt"),
        ("кинетического", "kinetic"), ("кинетический", "kinetic"), ("кинетическим", "kinetic"),
        ("огнестрельного", "ballistic"), ("огнестрельный", "ballistic"), ("огнестрельным", "ballistic"),
        ("термического", "thermal"), ("термический", "thermal"), ("термическим", "thermal"),
        ("энергетического", "energy"), ("энергетический", "energy"), ("энергетическим", "energy"),
        ("химического", "chemical"), ("химический", "chemical"), ("химическим", "chemical"),
        ("некротического", "necrotic"), ("некротический", "necrotic"), ("некротическим", "necrotic"), ("некротика", "necrotic"), ("некротикой", "necrotic"),
        ("психического", "psychic"), ("психический", "psychic"), ("психическим", "psychic"), ("психика", "psychic"),
        ("сияющего", "radiant"), ("сияющий", "radiant"), ("сияющим", "radiant"), ("светом", "radiant"), ("свет", "radiant"),
        ("магического", "magical"), ("магический", "magical"), ("магическим", "magical"),
        ("звукового", "sonic"), ("звуковой", "sonic"), ("звуковым", "sonic"), ("звук", "thunder"), ("звуком", "thunder"),
        ("огненного", "fire"), ("огненный", "fire"), ("огнём", "fire"), ("огнем", "fire"), ("огня", "fire"), ("огонь", "fire"),
        ("холодом", "cold"), ("холода", "cold"), ("холод", "cold"), ("крио", "cryo"),
        ("электричеством", "lightning"), ("электричества", "lightning"), ("электричество", "lightning"),
        ("ядом", "poison"), ("яда", "poison"), ("яд", "poison"),
        ("кислотой", "acid"), ("кислоты", "acid"), ("кислота", "acid"),
        ("стихией", "elemental"), ("стихия", "elemental"),
        ("силовым полем", "force"), ("силовое поле", "force"), ("силовой", "force"),
        ("взрывом", "explosive"), ("взрыва", "explosive"), ("взрыв", "explosive"),
        ("радиацией", "radiation"), ("радиации", "radiation"), ("радиация", "radiation"),
        ("кибератакой", "cyberattack"), ("кибератаки", "cyberattack"), ("кибератака", "cyberattack"),
        ("ЭМИ", "EMP"), ("токсином", "toxin"), ("токсина", "toxin"), ("токсин", "toxin"),
    };

    private static readonly (Regex Rx, string En)[] Compiled =
        Words.Select(w => (new Regex($@"(?<![\p{{L}}]){Regex.Escape(w.Ru)}(?![\p{{L}}])", RegexOptions.IgnoreCase), w.En)).ToArray();

    /// <summary>Переводит русские типы урона в тексте на английский, если интерфейс английский; иначе возвращает как есть.</summary>
    public static string Localize(string? text)
    {
        if (string.IsNullOrEmpty(text) || !Lang.IsEn)
        {
            return text ?? "";
        }

        var s = text;
        foreach (var (rx, en) in Compiled)
        {
            s = rx.Replace(s, en);
        }

        return s;
    }
}
