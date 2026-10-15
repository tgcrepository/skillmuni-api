using Learner.Application.Contracts;
using Learner.Application.Errors;
using SkillMuni.Common.Results;

namespace Learner.Application.Services;

/// <summary>
/// Decides a file's type from its first bytes, not from the name or the browser's Content-Type, so a renamed file
/// can't slip through. Returns the buffered content so the upload stream is read once.
/// </summary>
internal static class FileSniffer
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";
    public const string Heic = "image/heic";
    public const string Pdf = "application/pdf";

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static readonly string[] Images = [Jpeg, Png, Webp, Heic];

    public static readonly string[] Certificates = [Pdf, Jpeg, Png, Webp, Heic];

    public static async Task<Result<(MemoryStream Content, string ContentType)>> ReadAsync(
        UploadedFile? file,
        long maxBytes,
        string[] allowed,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return LearnerErrors.FileMissing;
        }

        if (file.Length > maxBytes)
        {
            return LearnerErrors.FileTooLarge;
        }

        var buffer = new MemoryStream(capacity: (int)file.Length);
        await file.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > maxBytes)
        {
            await buffer.DisposeAsync();
            return LearnerErrors.FileTooLarge;
        }

        var type = Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)));
        if (type is null || !allowed.Contains(type, StringComparer.Ordinal))
        {
            await buffer.DisposeAsync();
            return LearnerErrors.UnsupportedType;
        }

        buffer.Position = 0;
        return (buffer, type);
    }

    internal static string? Detect(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(JpegMagic))
        {
            return Jpeg;
        }

        if (head.StartsWith(PngMagic))
        {
            return Png;
        }

        if (head.StartsWith("%PDF-"u8))
        {
            return Pdf;
        }

        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        if (head.Length >= 12 && head[4..8].SequenceEqual("ftyp"u8) &&
            (head[8..12].SequenceEqual("heic"u8) || head[8..12].SequenceEqual("heix"u8) || head[8..12].SequenceEqual("mif1"u8)))
        {
            return Heic;
        }

        return null;
    }
}
