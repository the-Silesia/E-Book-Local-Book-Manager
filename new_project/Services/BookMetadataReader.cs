using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;

namespace new_project.Services;

public static class BookMetadataReader
{
    public static async Task<string> ReadTitleAsync(StorageFile file, string fallbackTitle)
    {
        var extension = file.FileType;
        if (extension.Equals(".epub", System.StringComparison.OrdinalIgnoreCase))
        {
            var title = await ReadEpubTitleAsync(file);
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }
        else if (extension.Equals(".pdf", System.StringComparison.OrdinalIgnoreCase))
        {
            var title = await ReadPdfTitleAsync(file);
            if (title is not null && IsUsablePdfTitle(title))
            {
                return title;
            }
        }

        return fallbackTitle;
    }

    private static async Task<string?> ReadEpubTitleAsync(StorageFile file)
    {
        await using var stream = await file.OpenStreamForReadAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var containerEntry = archive.GetEntry("META-INF/container.xml");
        if (containerEntry is null)
        {
            return null;
        }

        using var containerStream = containerEntry.Open();
        var container = await XDocument.LoadAsync(containerStream, LoadOptions.None, default);
        var rootFile = container.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "rootfile")?
            .Attribute("full-path")?.Value;
        if (string.IsNullOrWhiteSpace(rootFile))
        {
            return null;
        }

        var packageEntry = archive.GetEntry(rootFile.Replace('\\', '/'));
        if (packageEntry is null)
        {
            return null;
        }

        using var packageStream = packageEntry.Open();
        var package = await XDocument.LoadAsync(packageStream, LoadOptions.None, default);
        return package.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim();
    }

    private static async Task<string?> ReadPdfTitleAsync(StorageFile file)
    {
        await using var stream = await file.OpenStreamForReadAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();
        var content = Encoding.Latin1.GetString(bytes);
        var marker = "/Title";
        var markerIndex = content.IndexOf(marker, System.StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var start = content.IndexOf('(', markerIndex + marker.Length);
        var end = start < 0 ? -1 : content.IndexOf(')', start + 1);
        if (start >= 0 && end > start)
        {
            var titleBytes = content[(start + 1)..end]
                .Select(character => (byte)character)
                .ToArray();
            return DecodePdfText(titleBytes);
        }

        var hexStart = content.IndexOf('<', markerIndex + marker.Length);
        var hexEnd = hexStart < 0 ? -1 : content.IndexOf('>', hexStart + 1);
        if (hexStart < 0 || hexEnd <= hexStart)
        {
            return null;
        }

        var hexValue = content[(hexStart + 1)..hexEnd]
            .Where(character => !char.IsWhiteSpace(character))
            .ToArray();
        if (hexValue.Length == 0 || hexValue.Length % 2 != 0)
        {
            return null;
        }

        var hexBytes = new byte[hexValue.Length / 2];
        for (var index = 0; index < hexBytes.Length; index++)
        {
            if (!byte.TryParse(
                    new string(hexValue, index * 2, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out hexBytes[index]))
            {
                return null;
            }
        }

        return DecodePdfText(hexBytes);
    }

    private static string? DecodePdfText(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2).Trim();
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2).Trim();
        }

        return Encoding.UTF8.GetString(bytes).Trim();
    }

    private static bool IsUsablePdfTitle(string? title)
    {
        return !string.IsNullOrWhiteSpace(title)
            && title.Any(character => !char.IsDigit(character));
    }
}