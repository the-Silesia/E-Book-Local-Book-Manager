using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace new_project.Services;

public sealed class FolderAccessService
{
    public string AddToFutureAccessList(StorageFolder folder)
    {
        return StorageApplicationPermissions.FutureAccessList.Add(folder);
    }

    public async Task<StorageFolder?> TryGetFolderAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070002))
        {
            return null;
        }
    }

    public async Task<StorageFile?> TryGetFileAsync(string? folderToken, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(folderToken) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var segments = SplitRelativePath(relativePath);
            if (segments.Count == 0)
            {
                return null;
            }

            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(folderToken);
            for (var index = 0; index < segments.Count - 1; index++)
            {
                folder = await folder.GetFolderAsync(segments[index]);
            }

            return await folder.GetFileAsync(segments[^1]);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070002))
        {
            return null;
        }
    }

    private static IReadOnlyList<string> SplitRelativePath(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                return [];
            }
        }

        return segments;
    }
}
