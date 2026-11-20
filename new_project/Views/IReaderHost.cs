namespace new_project.Views;

public interface IReaderHost
{
    void SetReaderTitle(string? title);

    bool ToggleReaderFullscreen();

    void ExitReaderFullscreen();
}

public sealed record ReaderPageNavigationParameter(int BookId, IReaderHost Host);
