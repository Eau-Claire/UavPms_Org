namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

/// <summary>Validates the actual media signature and minimal decodable container structure.</summary>
public static class InspectionMediaFileValidator
{
    public static bool IsValid(Stream? stream, string? contentType)
    {
        if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < 8)
            return false;

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            if (read < 8) return false;

            var type = contentType?.Trim().ToLowerInvariant();
            return type switch
            {
                "image/jpeg" => IsJpeg(stream, header),
                "image/png" => IsPng(stream, header),
                "image/webp" => IsWebP(stream, header),
                "image/tiff" => IsTiff(stream, header),
                "video/mp4" => IsMp4(stream, header),
                _ => false
            };
        }
        catch
        {
            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool IsJpeg(Stream stream, ReadOnlySpan<byte> header)
    {
        if (header[0] != 0xFF || header[1] != 0xD8) return false;
        stream.Position = stream.Length - 2;
        return stream.ReadByte() == 0xFF && stream.ReadByte() == 0xD9;
    }

    private static bool IsPng(Stream stream, ReadOnlySpan<byte> header)
    {
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || stream.Length < 20) return false;
        stream.Position = stream.Length - 8;
        Span<byte> tail = stackalloc byte[8];
        return stream.Read(tail) == 8 && tail.SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
    }

    private static bool IsWebP(Stream stream, ReadOnlySpan<byte> header)
    {
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WEBP"u8)) return false;
        var declaredLength = BitConverter.ToUInt32(header[4..8]);
        return declaredLength + 8 <= stream.Length;
    }

    private static bool IsTiff(Stream stream, ReadOnlySpan<byte> header)
    {
        var littleEndian = header[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 });
        var bigEndian = header[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A });
        if (!littleEndian && !bigEndian) return false;
        var offset = littleEndian
            ? BitConverter.ToUInt32(header[4..8])
            : (uint)(header[4] << 24 | header[5] << 16 | header[6] << 8 | header[7]);
        return offset >= 8 && offset < stream.Length;
    }

    private static bool IsMp4(Stream stream, ReadOnlySpan<byte> header)
    {
        if (!header[4..8].SequenceEqual("ftyp"u8)) return false;
        var boxSize = (uint)(header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3]);
        return boxSize >= 8 && boxSize <= stream.Length;
    }
}
