using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using new_project.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.ApplicationModel.Resources;

namespace new_project.Views;

public sealed class FavoriteGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? "\uE735" : "\uE734";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class BookTooltipConverter : IValueConverter
{
    private static readonly ResourceLoader ResourceLoader = ResourceLoader.GetForViewIndependentUse();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not Book book)
        {
            return string.Empty;
        }

        var lines = new List<string> { book.Title };
        var progress = ParseProgress(book.ReadingProgress);
        if (progress > 0)
        {
            lines.Add(string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoader.GetString("HomePage_BookToolTip_ReadingProgress"),
                progress.ToString("P0", CultureInfo.CurrentCulture)));
        }

        if (book.DateLastOpened.HasValue)
        {
            lines.Add(GetLastOpenedText(book.DateLastOpened.Value));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }

    private static double ParseProgress(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var progress)
            ? Math.Clamp(progress, 0, 1)
            : 0;
    }

    private static string GetLastOpenedText(DateTimeOffset dateLastOpened)
    {
        var elapsed = DateTimeOffset.UtcNow - dateLastOpened.ToUniversalTime();
        if (elapsed.TotalDays >= 1)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoader.GetString("HomePage_BookToolTip_LastOpened"),
                dateLastOpened.ToLocalTime().ToString(
                    ResourceLoader.GetString("HomePage_BookToolTip_DateFormat"),
                    CultureInfo.CurrentCulture));
        }

        if (elapsed.TotalHours >= 1)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoader.GetString("HomePage_BookToolTip_LastOpenedHours"),
                Math.Floor(elapsed.TotalHours));
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoader.GetString("HomePage_BookToolTip_LastOpenedMinutes"),
                Math.Floor(elapsed.TotalMinutes));
        }

        return ResourceLoader.GetString("HomePage_BookToolTip_LastOpenedWithinMinute");
    }
}

public sealed class ReadingProgressVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var progress = value is double doubleProgress
            ? doubleProgress
            : double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0d;

        return progress > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed class ReadingProgressConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double progress)
        {
            return Math.Clamp(progress, 0, 1) * 100;
        }

        return double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0, 1) * 100
            : 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}