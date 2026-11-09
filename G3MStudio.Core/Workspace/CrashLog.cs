using System.Globalization;
using System.Reflection;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The local crash log (UTMT-APP-14): an unhandled exception is written to a text file in the user data folder, so a user can attach it to a report whether or not
/// Sentry reporting is on. Nothing leaves the machine. The newest <see cref="Keep"/> files stay.
/// </summary>
public static class CrashLog
{
    public const int Keep = 10;

    /// <summary>Writes the exception and returns the file, or null when the folder cannot be written (a crash log must never throw).</summary>
    public static string? Write(UserData data, Exception exception)
    {
        try
        {
            var folder = Directory.CreateDirectory(data.Combine("crashes")).FullName;
            var path = Path.Combine(folder, $"crash-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString("N")[..4]}.txt");
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            File.WriteAllText(path, $"G3MStudio {version}\n{Environment.OSVersion}\n{DateTime.UtcNow:O}\n\n{exception}\n");
            foreach (var old in Directory.EnumerateFiles(folder, "crash-*.txt").Order(StringComparer.Ordinal).SkipLast(Keep)) File.Delete(old);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
