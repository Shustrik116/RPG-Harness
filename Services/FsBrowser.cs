namespace RPG_Harness.Services;

/// <summary>Небольшой помощник для обхода файловой системы (выбор рабочей директории).</summary>
public static class FsBrowser
{
    /// <summary>Готовые к использованию диски (например, "C:\", "D:\").</summary>
    public static List<string> GetDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.Name)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>Имена подкаталогов; при ошибке доступа — пусто + текст ошибки.</summary>
    public static (List<string> Directories, string? Error) Browse(string path)
    {
        try
        {
            var dirs = Directory.GetDirectories(path)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (dirs, null);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                   or IOException
                                   or System.Security.SecurityException
                                   or ArgumentException
                                   or NotSupportedException)
        {
            return (new List<string>(), ex.Message);
        }
    }
}