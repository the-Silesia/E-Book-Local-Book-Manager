using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using new_project.Models;
using new_project.ViewModels;
using Windows.ApplicationModel.Resources;

namespace new_project.Views;

public sealed partial class FavoritePage : Page, IBookSearchPage
{
    private BooksViewModel ViewModel => App.LibraryViewModel;
    private FavoritePageViewModel FavoriteViewModel { get; }
    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();

    public FavoritePage()
    {
        InitializeComponent();
        FavoriteViewModel = new FavoritePageViewModel(ViewModel);
        FavoriteGridView.DataContext = FavoriteViewModel;
    }

    public void SetBookSearchQuery(string? query)
    {
        FavoriteViewModel.SetSearchQuery(query);
        UpdateViewState();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            await ViewModel.LoadAsync();
            FavoriteViewModel.Refresh();
            UpdateViewState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private void FavoriteGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Book book)
        {
                if (App.CurrentWindow is MainWindow mainWindow)
                {
                    mainWindow.OpenBook(book);
                }
        }
    }

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not Book book)
        {
            return;
        }

        try
        {
            await ViewModel.SetFavoriteAsync(book, false);
            FavoriteViewModel.Refresh();
            UpdateViewState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private void UpdateViewState()
    {
        var hasFavorites = FavoriteViewModel.FavoriteBooks.Count > 0;
        FavoritePage_nullContent_Info.Visibility = hasFavorites
            ? Visibility.Collapsed
            : Visibility.Visible;
        FavoritePageContent.Visibility = hasFavorites
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async Task ShowErrorAsync(string message)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = GetString("HomePage_ContentDialog_ImportError_Title"),
            Content = message,
            CloseButtonText = GetString("HomePage_ContentDialog_ImportError_CloseButtonText")
        };
        await dialog.ShowAsync();
    }

    private string GetString(string resourceKey)
    {
        return _resourceLoader.GetString(resourceKey);
    }
}
