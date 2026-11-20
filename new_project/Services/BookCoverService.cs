using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace new_project.Services;

public sealed class BookCoverService
{
    private const uint CoverWidth = 480;
    private const uint CoverHeight = 678;
    private const string CoverFolderName = "Covers";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    public static string CacheFolderPath => Path.Combine(
        ApplicationData.Current.LocalFolder.Path,
        CoverFolderName);

    public async Task ClearCoverCacheAsync()
    {
        if (!Directory.Exists(CacheFolderPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(CacheFolderPath, "*.png", SearchOption.TopDirectoryOnly))
        {
            File.Delete(filePath);
        }

        await Task.CompletedTask;
    }

    public async Task<string?> CreateImageFolderCoverAsync(StorageFolder folder)
    {
        try
        {
            var imageFile = (await folder.GetFilesAsync())
                .Where(file => ImageExtensions.Contains(file.FileType))
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return imageFile is null
                ? null
                : await CreateImageCoverAsync(imageFile);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> CreateEpubCoverAsync(StorageFile file)
    {
        try
        {
            await using var epubStream = await file.OpenStreamForReadAsync();
            using var archive = new ZipArchive(epubStream, ZipArchiveMode.Read);
            var containerEntry = FindArchiveEntry(archive, "META-INF/container.xml");
            if (containerEntry is null)
            {
                return null;
            }

            using var containerStream = containerEntry.Open();
            var container = await XDocument.LoadAsync(containerStream, LoadOptions.None, default);
            var opfPath = container.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "rootfile")?
                .Attribute("full-path")?.Value;
            opfPath = NormalizeArchivePath(opfPath);
            if (opfPath is null)
            {
                return null;
            }

            var packageEntry = FindArchiveEntry(archive, opfPath);
            if (packageEntry is null)
            {
                return null;
            }

            using var packageStream = packageEntry.Open();
            var package = await XDocument.LoadAsync(packageStream, LoadOptions.None, default);
            var manifest = package.Descendants()
                .Where(element => element.Name.LocalName == "item")
                .Select(element => new EpubManifestItem(
                    element.Attribute("id")?.Value ?? string.Empty,
                    ResolveArchivePath(opfPath, element.Attribute("href")?.Value),
                    element.Attribute("media-type")?.Value ?? string.Empty,
                    element.Attribute("properties")?.Value ?? string.Empty))
                .Where(item => item.Path is not null)
                .ToArray();

            var coverItem = manifest.FirstOrDefault(item =>
                item.Properties.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Contains("cover-image", StringComparer.OrdinalIgnoreCase));
            if (coverItem is null)
            {
                var coverId = package.Descendants()
                    .Where(element => element.Name.LocalName == "meta")
                    .Where(element => string.Equals(
                        element.Attribute("name")?.Value,
                        "cover",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(element => element.Attribute("content")?.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                coverItem = manifest.FirstOrDefault(item =>
                    string.Equals(item.Id, coverId, StringComparison.Ordinal));
            }

            var imageItem = coverItem is not null && IsSupportedImage(coverItem)
                ? coverItem
                : manifest
                    .Where(IsSupportedImage)
                    .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            if (imageItem?.Path is null)
            {
                return null;
            }

            var imageEntry = FindArchiveEntry(archive, imageItem.Path);
            return imageEntry is null
                ? null
                : await CreateArchiveImageCoverAsync(file, imageEntry);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> CreatePdfCoverAsync(StorageFile file)
    {
        try
        {
            var cacheFile = await GetCacheFileAsync(await GetCacheKeyAsync(file));
            if (cacheFile is not null)
            {
                return cacheFile.Path;
            }

            await using var sourceStream = await file.OpenStreamForReadAsync();
            using var randomAccessStream = sourceStream.AsRandomAccessStream();
            var document = await PdfDocument.LoadFromStreamAsync(randomAccessStream);
            if (document.PageCount == 0)
            {
                return null;
            }

            cacheFile = await CreateCacheFileAsync(await GetCacheKeyAsync(file));
            await RenderPdfPageAsync(document.GetPage(0), cacheFile);
            return cacheFile.Path;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> CreateZipCoverAsync(StorageFile archiveFile)
    {
        try
        {
            await using var archiveStream = await archiveFile.OpenStreamForReadAsync();
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            if (archive.Entries.Any(entry => entry.FullName.Contains('/') || entry.FullName.Contains('\\')))
            {
                return null;
            }

            var imageEntry = archive.Entries
                .Where(entry => ImageExtensions.Contains(Path.GetExtension(entry.Name)))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (imageEntry is null)
            {
                return null;
            }

            var cacheKey = await GetCacheKeyAsync(archiveFile, imageEntry.FullName);
            var cacheFile = await GetCacheFileAsync(cacheKey);
            if (cacheFile is not null)
            {
                return cacheFile.Path;
            }

            using var entryStream = imageEntry.Open();
            using var imageStream = new MemoryStream();
            await entryStream.CopyToAsync(imageStream);
            imageStream.Position = 0;
            cacheFile = await CreateCacheFileAsync(cacheKey);
            await CreateImageCoverAsync(imageStream.AsRandomAccessStream(), cacheFile);
            return cacheFile.Path;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> CreateImageCoverAsync(StorageFile file)
    {
        var cacheKey = await GetCacheKeyAsync(file);
        var cacheFile = await GetCacheFileAsync(cacheKey);
        if (cacheFile is not null)
        {
            return cacheFile.Path;
        }

        await using var sourceStream = await file.OpenStreamForReadAsync();
        using var randomAccessStream = sourceStream.AsRandomAccessStream();
        cacheFile = await CreateCacheFileAsync(cacheKey);
        await CreateImageCoverAsync(randomAccessStream, cacheFile);
        return cacheFile.Path;
    }

    private static async Task CreateImageCoverAsync(
        IRandomAccessStream sourceStream,
        StorageFile outputFile)
    {
        var decoder = await BitmapDecoder.CreateAsync(sourceStream);
        var scale = Math.Max(
            (double)CoverWidth / decoder.PixelWidth,
            (double)CoverHeight / decoder.PixelHeight);
        var scaledWidth = (uint)Math.Ceiling(decoder.PixelWidth * scale);
        var scaledHeight = (uint)Math.Ceiling(decoder.PixelHeight * scale);
        var transform = new BitmapTransform
        {
            ScaledWidth = scaledWidth,
            ScaledHeight = scaledHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
            Bounds = new BitmapBounds
            {
                X = (scaledWidth - CoverWidth) / 2,
                Y = (scaledHeight - CoverHeight) / 2,
                Width = CoverWidth,
                Height = CoverHeight
            }
        };

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);
        await SavePngAsync(bitmap, outputFile);
    }

    private static async Task<string?> CreateArchiveImageCoverAsync(
        StorageFile archiveFile,
        ZipArchiveEntry imageEntry)
    {
        var entryPath = NormalizeArchivePath(imageEntry.FullName);
        if (entryPath is null)
        {
            return null;
        }

        var cacheKey = await GetCacheKeyAsync(archiveFile, entryPath);
        var cacheFile = await GetCacheFileAsync(cacheKey);
        if (cacheFile is not null)
        {
            return cacheFile.Path;
        }

        using var entryStream = imageEntry.Open();
        using var imageStream = new MemoryStream();
        await entryStream.CopyToAsync(imageStream);
        imageStream.Position = 0;
        cacheFile = await CreateCacheFileAsync(cacheKey);
        using var randomAccessStream = imageStream.AsRandomAccessStream();
        await CreateImageCoverAsync(randomAccessStream, cacheFile);
        return cacheFile.Path;
    }

    private static async Task RenderPdfPageAsync(PdfPage page, StorageFile outputFile)
    {
        var pageSize = page.Size;
        var targetAspectRatio = (double)CoverWidth / CoverHeight;
        var pageAspectRatio = pageSize.Width / pageSize.Height;
        Rect sourceRect;
        if (pageAspectRatio > targetAspectRatio)
        {
            var cropWidth = pageSize.Height * targetAspectRatio;
            sourceRect = new Rect((pageSize.Width - cropWidth) / 2, 0, cropWidth, pageSize.Height);
        }
        else
        {
            var cropHeight = pageSize.Width / targetAspectRatio;
            sourceRect = new Rect(0, (pageSize.Height - cropHeight) / 2, pageSize.Width, cropHeight);
        }

        using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        var options = new PdfPageRenderOptions
        {
            BitmapEncoderId = BitmapEncoder.PngEncoderId,
            DestinationWidth = CoverWidth,
            DestinationHeight = CoverHeight,
            SourceRect = sourceRect,
            BackgroundColor = Color.FromArgb(255, 255, 255, 255),
            IsIgnoringHighContrast = true
        };
        await page.RenderToStreamAsync(outputStream, options);
        await outputStream.FlushAsync();
    }

    private static async Task SavePngAsync(SoftwareBitmap bitmap, StorageFile outputFile)
    {
        using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetSoftwareBitmap(bitmap);
        encoder.IsThumbnailGenerated = false;
        await encoder.FlushAsync();
    }

    private static async Task<StorageFile> CreateCacheFileAsync(string cacheKey)
    {
        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
            CoverFolderName,
            CreationCollisionOption.OpenIfExists);
        return await folder.CreateFileAsync(
            $"{cacheKey}.png",
            CreationCollisionOption.ReplaceExisting);
    }

    private static async Task<StorageFile?> GetCacheFileAsync(string cacheKey)
    {
        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
            CoverFolderName,
            CreationCollisionOption.OpenIfExists);
        try
        {
            return await folder.GetFileAsync($"{cacheKey}.png");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static async Task<string> GetCacheKeyAsync(StorageFile file, string? entryPath = null)
    {
        var properties = await file.GetBasicPropertiesAsync();
        var value = string.Join(
            "|",
            "v2",
            file.Path,
            properties.Size,
            properties.DateModified.UtcDateTime.Ticks,
            entryPath ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static ZipArchiveEntry? FindArchiveEntry(ZipArchive archive, string path)
    {
        var normalizedPath = NormalizeArchivePath(path);
        return normalizedPath is null
            ? null
            : archive.Entries.FirstOrDefault(entry =>
                string.Equals(
                    NormalizeArchivePath(entry.FullName),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveArchivePath(string opfPath, string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var cleanHref = href.Split('#', 2)[0].Split('?', 2)[0];
        var opfDirectory = opfPath[..Math.Max(0, opfPath.LastIndexOf('/'))];
        return NormalizeArchivePath(string.IsNullOrEmpty(opfDirectory)
            ? cleanHref
            : $"{opfDirectory}/{cleanHref}");
    }

    private static string? NormalizeArchivePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = Uri.UnescapeDataString(path)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalizedSegments = new List<string>();
        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (normalizedSegments.Count == 0)
                {
                    return null;
                }

                normalizedSegments.RemoveAt(normalizedSegments.Count - 1);
                continue;
            }

            normalizedSegments.Add(segment);
        }

        return normalizedSegments.Count == 0
            ? null
            : string.Join('/', normalizedSegments);
    }

    private static bool IsSupportedImage(EpubManifestItem item)
    {
        return item.Path is not null
            && (ImageExtensions.Contains(Path.GetExtension(item.Path))
                || item.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record EpubManifestItem(
        string Id,
        string? Path,
        string MediaType,
        string Properties);
}
