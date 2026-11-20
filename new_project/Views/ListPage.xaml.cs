using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using new_project.Models;
using new_project.Services;
using new_project.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.ApplicationModel.Resources;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace new_project.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListPage : Page
{
    private ListPageViewModel ViewModel { get; }
    private readonly FolderAccessService _folderAccessService = new();
    private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForViewIndependentUse();
    private BookList? _contextBookList;

    public ListPage()
    {
        InitializeComponent();
        ViewModel = new ListPageViewModel(App.LibraryViewModel);
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await ViewModel.LoadAsync();
            UpdateViewState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private async void AddBookListButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is null)
        {
            return;
        }

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.CurrentWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        try
        {
            var token = _folderAccessService.AddToFutureAccessList(folder);
            var scanResults = await ViewModel.ScanBookListAsync(folder, token);
            if (await ShowImportConfirmationAsync(scanResults.Count) != ContentDialogResult.Primary)
            {
                return;
            }

            await ViewModel.ImportBookListAsync(folder, token, scanResults);
            UpdateViewState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private void BookListItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is BookList bookList)
        {
            _contextBookList = bookList;
        }
    }

    private async void RefreshBookListMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(ViewModel.RefreshAsync);
    }

    private async void RenameBookListMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextBookList is not null)
        {
            await RenameBookListAsync(_contextBookList);
        }
    }

    private async void DeleteBookListMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextBookList is null)
        {
            return;
        }

        var result = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = GetString("ListPage_ContentDialog_Delete_Title"),
            Content = string.Format(
                GetString("ListPage_ContentDialog_Delete_Content"),
                _contextBookList.ListName),
            PrimaryButtonText = GetString("ListPage_ContentDialog_Delete_PrimaryButtonText"),
            CloseButtonText = GetString("ListPage_ContentDialog_Delete_CloseButtonText"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await result.ShowAsync() == ContentDialogResult.Primary)
        {
            await ExecuteAsync(() => ViewModel.DeleteAsync(_contextBookList));
        }
    }

    private async Task RenameBookListAsync(BookList bookList)
    {
        var textBox = new TextBox { Text = bookList.ListName };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = GetString("ListPage_ContentDialog_Rename_Title"),
            Content = textBox,
            PrimaryButtonText = GetString("ListPage_ContentDialog_Rename_PrimaryButtonText"),
            CloseButtonText = GetString("ListPage_ContentDialog_Rename_CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ExecuteAsync(() => ViewModel.RenameAsync(bookList, textBox.Text));
        }
    }

    private async Task<ContentDialogResult> ShowImportConfirmationAsync(int bookCount)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = GetString("ListPage_ContentDialog_Import_Title"),
            Content = string.Format(GetString("ListPage_ContentDialog_Import_Content"), bookCount),
            PrimaryButtonText = GetString("ListPage_ContentDialog_Import_PrimaryButtonText"),
            CloseButtonText = GetString("ListPage_ContentDialog_Import_CloseButtonText"),
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync();
    }

    private async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            UpdateViewState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(exception.Message);
        }
    }

    private void UpdateViewState()
    {
        var hasBookLists = ViewModel.BookLists.Count > 0;
        ListPage_nullContent_Info.Visibility = hasBookLists ? Visibility.Collapsed : Visibility.Visible;
        BookListsView.Visibility = hasBookLists ? Visibility.Visible : Visibility.Collapsed;
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
            Title = GetString("ListPage_ContentDialog_Error_Title"),
            Content = message,
            CloseButtonText = GetString("ListPage_ContentDialog_Error_CloseButtonText")
        };
        await dialog.ShowAsync();
    }

    private string GetString(string resourceKey)
    {
        return _resourceLoader.GetString(resourceKey);
    }
}
