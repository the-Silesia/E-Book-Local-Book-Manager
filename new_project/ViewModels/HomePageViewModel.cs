using CommunityToolkit.Mvvm.ComponentModel;
using new_project.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace new_project.ViewModels;

public enum HomePageSortMode
{
    LastRead,
    AddedDescending,
    AddedAscending,
    NameAscending,
    NameDescending
}

public partial class HomePageViewModel : ObservableObject
{
    private readonly BooksViewModel _libraryViewModel;
    private readonly HashSet<int> _selectedBookListIds = new();
    private HomePageSortMode _sortMode = HomePageSortMode.LastRead;
    private string _searchQuery = string.Empty;

    public ObservableCollection<Book> DisplayedBooks { get; } = new();

    public IReadOnlySet<int> SelectedBookListIds => _selectedBookListIds;

    public HomePageSortMode SortMode => _sortMode;

    public string SearchQuery => _searchQuery;

    public HomePageViewModel(BooksViewModel libraryViewModel)
    {
        _libraryViewModel = libraryViewModel;
    }

    public void Refresh()
    {
        var bookListIds = _libraryViewModel.BookLists
            .Select(bookList => bookList.Id)
            .ToHashSet();

        _selectedBookListIds.IntersectWith(bookListIds);
        if (_selectedBookListIds.Count == 0 && bookListIds.Count > 0)
        {
            _selectedBookListIds.UnionWith(bookListIds);
        }

        ApplyFiltersAndSort();
    }

    public void SetSearchQuery(string? query)
    {
        _searchQuery = query?.Trim() ?? string.Empty;
        ApplyFiltersAndSort();
    }

    public void SetSortMode(HomePageSortMode sortMode)
    {
        _sortMode = sortMode;
        OnPropertyChanged(nameof(SortMode));
        ApplyFiltersAndSort();
    }

    public void SetBookListSelected(int bookListId, bool isSelected)
    {
        if (isSelected)
        {
            _selectedBookListIds.Add(bookListId);
        }
        else
        {
            _selectedBookListIds.Remove(bookListId);
        }

        ApplyFiltersAndSort();
    }

    public void SetAllBookListsSelected(bool isSelected)
    {
        _selectedBookListIds.Clear();
        if (isSelected)
        {
            _selectedBookListIds.UnionWith(_libraryViewModel.BookLists.Select(bookList => bookList.Id));
        }

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        IEnumerable<Book> books = _libraryViewModel.Books;
        var allBookListIds = _libraryViewModel.BookLists
            .Select(bookList => bookList.Id)
            .ToHashSet();

        if (allBookListIds.Count > 0 && _selectedBookListIds.Count < allBookListIds.Count)
        {
            var selectedBookIds = _libraryViewModel.BookLists
                .Where(bookList => _selectedBookListIds.Contains(bookList.Id))
                .SelectMany(bookList => bookList.Books)
                .Select(book => book.Id)
                .ToHashSet();
            books = books.Where(book => selectedBookIds.Contains(book.Id));
        }

        if (_searchQuery.Length > 0)
        {
            books = books.Where(book => book.Title.Contains(_searchQuery, StringComparison.CurrentCultureIgnoreCase));
        }

        books = _sortMode switch
        {
            HomePageSortMode.AddedDescending => books
                .OrderByDescending(book => book.DateAdded)
                .ThenBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase),
            HomePageSortMode.AddedAscending => books
                .OrderBy(book => book.DateAdded)
                .ThenBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase),
            HomePageSortMode.NameAscending => books
                .OrderBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase),
            HomePageSortMode.NameDescending => books
                .OrderByDescending(book => book.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => books
                .OrderByDescending(book => book.DateLastOpened ?? DateTimeOffset.MinValue)
                .ThenByDescending(book => book.DateAdded)
        };

        DisplayedBooks.Clear();
        foreach (var book in books)
        {
            DisplayedBooks.Add(book);
        }
    }
}
