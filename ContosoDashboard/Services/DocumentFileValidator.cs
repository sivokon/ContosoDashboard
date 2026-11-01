namespace ContosoDashboard.Services;

public static class DocumentFileValidator
{
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;
    public const int HeaderBytesNeeded = 512;

    private static readonly Dictionary<string, string> MimeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46 };
    private static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] Ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly HashSet<string> PreviewableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png"
    };

    public static string SupportedTypesDescription =>
        "PDF, Word, Excel, PowerPoint, text (.txt), JPEG and PNG files";

    public static string MaxSizeDescription => "25 MB";

    // Returns a lower-case extension including the dot (empty when there is none)
    public static string GetExtension(string fileName) =>
        Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

    public static bool IsSupportedExtension(string extension) =>
        MimeByExtension.ContainsKey(extension);

    public static string? GetMimeType(string extension) =>
        MimeByExtension.TryGetValue(extension, out var mime) ? mime : null;

    public static bool IsPreviewable(string? fileType) =>
        fileType != null && PreviewableTypes.Contains(fileType);

    // Validates name, size and (when the header is supplied) the content signature. Returns an error message or null.
    public static string? Validate(string fileName, long length, ReadOnlySpan<byte> header)
    {
        var extension = GetExtension(fileName);
        if (!IsSupportedExtension(extension))
            return $"Unsupported file type. Supported types: {SupportedTypesDescription}.";

        if (length <= 0)
            return "The file is empty.";

        if (length > MaxFileSizeBytes)
            return $"The file is larger than the {MaxSizeDescription} limit.";

        if (!SignatureMatches(extension, header))
            return "The file content does not match its file type.";

        return null;
    }

    public static bool SignatureMatches(string extension, ReadOnlySpan<byte> header)
    {
        switch (extension)
        {
            case ".pdf":
                return header.StartsWith(Pdf);
            case ".docx":
            case ".xlsx":
            case ".pptx":
                return header.StartsWith(Zip);
            case ".doc":
            case ".xls":
            case ".ppt":
                return header.StartsWith(Ole);
            case ".jpg":
            case ".jpeg":
                return header.StartsWith(Jpeg);
            case ".png":
                return header.StartsWith(Png);
            case ".txt":
                return !header.Contains((byte)0);
            default:
                return false;
        }
    }
}
