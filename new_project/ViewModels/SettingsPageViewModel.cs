using System;
using System.Collections.Generic;
using System.Text;
using new_project.Services;

namespace new_project.ViewModels;

public enum ReaderOpenMode
{
    ReaderWindow,
    MainWindow
}

public class SettingsPageViewModel
{
    public ReaderOpenMode ReaderOpenMode
    {
        get => AppSettingsService.Current.ReaderOpenMode;
        set => AppSettingsService.Current.ReaderOpenMode = value;
    }
}
