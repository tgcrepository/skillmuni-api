using Careers.Application.Contracts;
using SkillMuni.Common.Results;

namespace Careers.Application.Services;

/// <summary>
/// Types an uploaded CV from its first bytes (PDF, Word .docx, Word 97 .doc), not from its name, and keeps it under
/// the size limit (FR-42: 5 MB). Returns the buffered content so the upload is read once.
/// </summary>
internal static class CvFileSniffer
{
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Doc = "application/msword";

    private const int MaxNameLength = 120;

    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] OleMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static async Task<Result<(MemoryStream Content, string ContentType)>> ReadAsync(UploadedFile? file, long maxBytes, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return CareersErrors.FileMissing;
        }

        if (file.Length > maxBytes)
        {
            return CareersErrors.TooLarge;
        }

        var buffer = new MemoryStream((int)file.Length);
        await file.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > maxBytes || buffer.Length == 0)
        {
            await buffer.DisposeAsync();
            return buffer.Length == 0 ? CareersErrors.FileMissing : CareersErrors.TooLarge;
        }

        var type = Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)));
        if (type is null)
        {
            await buffer.DisposeAsync();
            return CareersErrors.UnsupportedType;
        }

        buffer.Position = 0;
        return (buffer, type);
    }

    public static string? Detect(ReadOnlySpan<byte> head) =>
        head.StartsWith(PdfMagic) ? Pdf
        : head.StartsWith(ZipMagic) ? Docx
        : head.StartsWith(OleMagic) ? Doc
        : null;

    /// <summary>The file name without any path or control characters, with the extension its type deserves.</summary>
    public static string CleanName(string fileName, string contentType)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        var extension = contentType switch
        {
            Pdf => ".pdf",
            Docx => ".docx",
            _ => ".doc",
        };
        if (name.Length == 0)
        {
            name = "My_CV" + extension;
        }

        return name.Length <= MaxNameLength ? name : name[..(MaxNameLength - extension.Length)] + extension;
    }
}
