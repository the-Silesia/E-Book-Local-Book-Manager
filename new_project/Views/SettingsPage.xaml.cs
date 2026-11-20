using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using new_project.Services;
using new_project.ViewModels;
using System.IO;
using Windows.ApplicationModel.Resources;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace new_project.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private static readonly ResourceLoader ResourceLoader = ResourceLoader.GetForViewIndependentUse();
    private MainWindow? MainWindow => App.CurrentWindow as MainWindow;
    private readonly BookCoverService _bookCoverService = new();

    public SettingsPage()
    {
        InitializeComponent();
        AddThemeOptions();
        AddBackdropOptions();
        ReaderOpenModeToggleSwitch.IsOn =
            AppSettingsService.Current.ReaderOpenMode == ReaderOpenMode.ReaderWindow;
    }

    private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem { Tag: AppThemeMode theme })
        {
            MainWindow?.SetTheme(theme);
            await AppSettingsService.SaveAsync();
        }
    }

    private async void BackdropComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackdropComboBox.SelectedItem is ComboBoxItem { Tag: AppBackdropMode backdrop })
        {
            MainWindow?.ApplyBackdrop(backdrop);
            await AppSettingsService.SaveAsync();
        }
    }

    private async void ReaderOpenModeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            AppSettingsService.Current.ReaderOpenMode = toggleSwitch.IsOn
                ? ReaderOpenMode.ReaderWindow
                : ReaderOpenMode.MainWindow;
            await AppSettingsService.SaveAsync();
        }
    }

    private void AddThemeOptions()
    {
        AddOption(ThemeComboBox, GetString("SettingsPage_ThemeOption_System"), AppThemeMode.System, AppSettingsService.Current.Theme);
        AddOption(ThemeComboBox, GetString("SettingsPage_ThemeOption_Light"), AppThemeMode.Light, AppSettingsService.Current.Theme);
        AddOption(ThemeComboBox, GetString("SettingsPage_ThemeOption_Dark"), AppThemeMode.Dark, AppSettingsService.Current.Theme);
    }

    private void AddBackdropOptions()
    {
        AddOption(BackdropComboBox, GetString("SettingsPage_BackdropOption_Mica"), AppBackdropMode.Mica, AppSettingsService.Current.Backdrop);
        AddOption(BackdropComboBox, GetString("SettingsPage_BackdropOption_MicaAlt"), AppBackdropMode.MicaAlt, AppSettingsService.Current.Backdrop);
        AddOption(BackdropComboBox, GetString("SettingsPage_BackdropOption_Acrylic"), AppBackdropMode.Acrylic, AppSettingsService.Current.Backdrop);
        AddOption(BackdropComboBox, GetString("SettingsPage_BackdropOption_AcrylicThin"), AppBackdropMode.AcrylicThin, AppSettingsService.Current.Backdrop);
    }

    private static void AddOption<T>(ComboBox comboBox, string content, T tag, T selectedTag)
    {
        var item = new ComboBoxItem
        {
            Content = content,
            Tag = tag
        };
        comboBox.Items.Add(item);
        if (Equals(tag, selectedTag))
        {
            comboBox.SelectedItem = item;
        }
    }

    private async void OpenCacheFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(BookCoverService.CacheFolderPath);
        await Launcher.LaunchFolderPathAsync(BookCoverService.CacheFolderPath);
    }

    private async void ClearCoverCacheButton_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = GetString("SettingsPage_ClearCoverCacheDialog_Title"),
            Content = GetString("SettingsPage_ClearCoverCacheDialog_Content"),
            PrimaryButtonText = GetString("SettingsPage_ClearCoverCacheDialog_PrimaryButtonText"),
            CloseButtonText = GetString("SettingsPage_ClearCoverCacheDialog_CloseButtonText"),
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _bookCoverService.ClearCoverCacheAsync();
        }
        catch (Exception exception)
        {
            var errorDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = GetString("SettingsPage_ClearCoverCacheErrorDialog_Title"),
                Content = exception.Message,
                CloseButtonText = GetString("SettingsPage_ClearCoverCacheErrorDialog_CloseButtonText")
            };
            await errorDialog.ShowAsync();
        }
    }

    private static string GetString(string resourceKey)
    {
        return ResourceLoader.GetString(resourceKey);
    }
}
