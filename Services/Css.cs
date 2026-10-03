using System.Globalization;

namespace RPG_Harness.Services;

/// <summary>
/// Куски inline-стилей, которые собираются в коде.
/// Числа здесь обязаны форматироваться через <see cref="CultureInfo.InvariantCulture"/>:
/// при русской локали <c>ToString("0.#")</c> даёт «85,7», а CSS с запятой невалиден —
/// браузер молча отбрасывает правило, и полоса становится нулевой ширины.
/// </summary>
public static class Css
{
    /// <summary>Ширина полосы в процентах от максимума: «width:85.7%». Максимум ≤ 0 → «width:0%».</summary>
    public static string Percent(int value, int max) =>
        max <= 0
            ? "width:0%"
            : "width:" +
              Math.Clamp(100.0 * Math.Max(value, 0) / max, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) +
              "%";
}
