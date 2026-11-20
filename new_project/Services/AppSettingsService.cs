using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using new_project.ViewModels;

namespace new_project.Services;

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public enum AppBackdropMode
{
    Mica,
    MicaAlt,
    Acrylic,
    AcrylicThin
}

public sealed class AppSettings
{
    public AppThemeMode Theme { get; set; } = AppThemeMode.System;
    public AppBackdropMode Backdrop { get; set; } = AppBackdropMode.Mica;
    public ReaderOpenMode ReaderOpenMode { get; set; } = ReaderOpenMode.ReaderWindow;
}

public static class AppSettingsService
{
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static AppSettings Current { get; private set; } = new();

    private static string SettingsPath => Path.Combine(
        ApplicationData.Current.LocalFolder.Path,
        SettingsFileName);

    public static async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            await using var stream = File.OpenRead(SettingsPath);
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions)
                ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, Current, JsonOptions);
    }
}
