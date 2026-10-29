namespace UavPms.OperationsService.Application.Common.Utilities;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

public record MediaMetadata(int? WidthPx, int? HeightPx, double? DurationSeconds, double? Fps);

/// <summary>
/// Zero-dependency extraction of the technical metadata stored on inspection_media:
/// SHA-256 checksum, pixel size of JPEG/PNG/WebP/TIFF images, and duration/size/frame rate of MP4 videos
/// (ISO BMFF boxes mvhd, tkhd, hdlr, mdhd, stts). Unknown or damaged files yield nulls, never exceptions.
/// The stream position is restored afterwards.
/// </summary>
public static class MediaMetadataExtractor
{
    private const int MaxBoxDepth = 8;

    /// <summary>Lower-case hex SHA-256 of the whole stream (64 chars, matches inspection_media.checksum).</summary>
    public static string ComputeSha256(Stream stream)
    {
        var initialPos = stream.Position;
        try
        {
            stream.Position = 0;
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        finally
        {
            stream.Position = initialPos;
        }
    }

    public static MediaMetadata Extract(Stream? stream, string? contentType)
    {
        var empty = new MediaMetadata(null, null, null, null);
        if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < 16)
        {
            return empty;
        }

        var initialPos = stream.Position;
        try
        {
            stream.Position = 0;
            return contentType?.Trim().ToLowerInvariant() switch
            {
                "image/jpeg" => ToImage(ReadJpegSize(stream)),
                "image/png" => ToImage(ReadPngSize(stream)),
                "image/webp" => ToImage(ReadWebPSize(stream)),
                "image/tiff" => ToImage(ReadTiffSize(stream)),
                "video/mp4" => ReadMp4(stream),
                _ => empty
            };
        }
        catch
        {
            return empty;
        }
        finally
        {
            stream.Position = initialPos;
        }
    }

    private static MediaMetadata ToImage((int Width, int Height)? size)
        => size is { Width: > 0, Height: > 0 } s ? new MediaMetadata(s.Width, s.Height, null, null) : new MediaMetadata(null, null, null, null);

    // ---------- Images ----------

    private static (int, int)? ReadJpegSize(Stream stream)
    {
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8) return null;
        Span<byte> buf = stackalloc byte[7];
        while (stream.Position < stream.Length - 4)
        {
            if (stream.ReadByte() != 0xFF) return null;
            var marker = stream.ReadByte();
            while (marker == 0xFF) marker = stream.ReadByte();
            if (marker is -1 or 0xD9 or 0xDA) return null;
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7)) continue; // markers without a length

            if (stream.Read(buf[..2]) != 2) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(buf);
            if (length < 2) return null;

            // SOF0..SOF15 except DHT (C4), JPG (C8) and DAC (CC): precision(1) height(2) width(2)
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (stream.Read(buf[..5]) != 5) return null;
                return (BinaryPrimitives.ReadUInt16BigEndian(buf[3..5]), BinaryPrimitives.ReadUInt16BigEndian(buf[1..3]));
            }

            stream.Position += length - 2;
        }
        return null;
    }

    private static (int, int)? ReadPngSize(Stream stream)
    {
        Span<byte> buf = stackalloc byte[24];
        if (stream.Read(buf) != 24 || !buf[12..16].SequenceEqual("IHDR"u8)) return null;
        return ((int)BinaryPrimitives.ReadUInt32BigEndian(buf[16..20]), (int)BinaryPrimitives.ReadUInt32BigEndian(buf[20..24]));
    }

    private static (int, int)? ReadWebPSize(Stream stream)
    {
        Span<byte> buf = stackalloc byte[30];
        if (stream.Read(buf) != 30 || !buf[..4].SequenceEqual("RIFF"u8) || !buf[8..12].SequenceEqual("WEBP"u8)) return null;
        var chunk = buf[12..16];
        if (chunk.SequenceEqual("VP8X"u8))
        {
            // 24-bit little-endian canvas size minus one
            return ((buf[24] | buf[25] << 8 | buf[26] << 16) + 1, (buf[27] | buf[28] << 8 | buf[29] << 16) + 1);
        }
        if (chunk.SequenceEqual("VP8 "u8))
        {
            // frame tag (3) + start code 9D 01 2A, then 14-bit width/height
            if (buf[23] != 0x9D || buf[24] != 0x01 || buf[25] != 0x2A) return null;
            return (BinaryPrimitives.ReadUInt16LittleEndian(buf[26..28]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(buf[28..30]) & 0x3FFF);
        }
        if (chunk.SequenceEqual("VP8L"u8))
        {
            if (buf[20] != 0x2F) return null;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(buf[21..25]);
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }
        return null;
    }

    private static (int, int)? ReadTiffSize(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        if (stream.Read(header) != 8) return null;
        bool le;
        if (header[0] == 0x49 && header[1] == 0x49) le = true;
        else if (header[0] == 0x4D && header[1] == 0x4D) le = false;
        else return null;

        uint U32(ReadOnlySpan<byte> s) => le ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);
        ushort U16(ReadOnlySpan<byte> s) => le ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);

        var ifd = U32(header[4..8]);
        if (ifd < 8 || ifd + 2 > stream.Length) return null;
        stream.Position = ifd;
        Span<byte> entry = stackalloc byte[12];
        if (stream.Read(entry[..2]) != 2) return null;
        var count = U16(entry[..2]);
        int? width = null, height = null;
        for (var i = 0; i < count && stream.Read(entry) == 12; i++)
        {
            var tag = U16(entry[..2]);
            if (tag is not (0x0100 or 0x0101)) continue;
            var type = U16(entry[2..4]);
            int value = type == 3 ? U16(entry[8..10]) : (int)U32(entry[8..12]); // SHORT or LONG
            if (tag == 0x0100) width = value; else height = value;
        }
        return width.HasValue && height.HasValue ? (width.Value, height.Value) : null;
    }

    // ---------- MP4 (ISO BMFF) ----------

    private sealed class TrackInfo
    {
        public string? Handler;
        public int? Width, Height;
        public uint Timescale;
        public ulong Duration;
        public ulong SampleCount;
    }

    private sealed class Mp4Info
    {
        public uint MovieTimescale;
        public ulong MovieDuration;
        public TrackInfo? Current;
        public TrackInfo? Video;
    }

    private static MediaMetadata ReadMp4(Stream stream)
    {
        var info = new Mp4Info();
        WalkBoxes(stream, 0, stream.Length, info, 0);

        double? duration = info.MovieTimescale > 0 && info.MovieDuration > 0
            ? Math.Round((double)info.MovieDuration / info.MovieTimescale, 3)
            : null;
        var video = info.Video;
        double? fps = null;
        if (video is { Timescale: > 0, Duration: > 0, SampleCount: > 0 })
        {
            fps = Math.Round(video.SampleCount / ((double)video.Duration / video.Timescale), 3);
        }
        return new MediaMetadata(video?.Width is > 0 ? video.Width : null, video?.Height is > 0 ? video.Height : null, duration, fps);
    }

    private static void WalkBoxes(Stream stream, long start, long end, Mp4Info info, int depth)
    {
        if (depth > MaxBoxDepth) return;
        Span<byte> head = stackalloc byte[16];
        var pos = start;
        while (pos + 8 <= end)
        {
            stream.Position = pos;
            if (stream.Read(head[..8]) != 8) return;
            long size = BinaryPrimitives.ReadUInt32BigEndian(head[..4]);
            var type = System.Text.Encoding.ASCII.GetString(head[4..8]);
            var headerLength = 8;
            if (size == 1)
            {
                if (stream.Read(head[8..16]) != 8) return;
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(head[8..16]);
                headerLength = 16;
            }
            else if (size == 0)
            {
                size = end - pos;
            }
            if (size < headerLength || pos + size > end) return;

            var bodyStart = pos + headerLength;
            var bodyLength = size - headerLength;
            switch (type)
            {
                case "moov" or "mdia" or "minf" or "stbl":
                    WalkBoxes(stream, bodyStart, bodyStart + bodyLength, info, depth + 1);
                    break;
                case "trak":
                    info.Current = new TrackInfo();
                    WalkBoxes(stream, bodyStart, bodyStart + bodyLength, info, depth + 1);
                    if (info.Current.Handler == "vide" && info.Video == null) info.Video = info.Current;
                    info.Current = null;
                    break;
                case "mvhd":
                    ReadTimescaleAndDuration(stream, bodyLength, out info.MovieTimescale, out info.MovieDuration);
                    break;
                case "mdhd" when info.Current != null:
                    ReadTimescaleAndDuration(stream, bodyLength, out info.Current.Timescale, out info.Current.Duration);
                    break;
                case "hdlr" when info.Current != null && bodyLength >= 12:
                    // version/flags(4) pre_defined(4) handler_type(4)
                    if (stream.Read(head[..12]) == 12) info.Current.Handler = System.Text.Encoding.ASCII.GetString(head[8..12]);
                    break;
                case "tkhd" when info.Current != null:
                    ReadTrackSize(stream, bodyLength, info.Current);
                    break;
                case "stts" when info.Current != null && bodyLength >= 8:
                    ReadSampleCount(stream, bodyLength, info.Current);
                    break;
            }
            pos += size;
        }
    }

    // mvhd / mdhd: version(1) flags(3), then v0: creation(4) modification(4) timescale(4) duration(4)
    //                                         v1: creation(8) modification(8) timescale(4) duration(8)
    private static void ReadTimescaleAndDuration(Stream stream, long bodyLength, out uint timescale, out ulong duration)
    {
        timescale = 0;
        duration = 0;
        Span<byte> buf = stackalloc byte[32];
        var version = stream.ReadByte();
        var needed = version == 1 ? 3 + 28 : 3 + 16;
        if (bodyLength < needed + 1 || stream.Read(buf[..needed]) != needed) return;
        if (version == 1)
        {
            timescale = BinaryPrimitives.ReadUInt32BigEndian(buf[19..23]);
            duration = BinaryPrimitives.ReadUInt64BigEndian(buf[23..31]);
        }
        else
        {
            timescale = BinaryPrimitives.ReadUInt32BigEndian(buf[11..15]);
            duration = BinaryPrimitives.ReadUInt32BigEndian(buf[15..19]);
        }
    }

    // tkhd: width/height are the last 8 bytes of the box, 16.16 fixed point.
    private static void ReadTrackSize(Stream stream, long bodyLength, TrackInfo track)
    {
        if (bodyLength < 84) return;
        var bodyStart = stream.Position;
        stream.Position = bodyStart + bodyLength - 8;
        Span<byte> buf = stackalloc byte[8];
        if (stream.Read(buf) != 8) return;
        track.Width = (int)(BinaryPrimitives.ReadUInt32BigEndian(buf[..4]) >> 16);
        track.Height = (int)(BinaryPrimitives.ReadUInt32BigEndian(buf[4..8]) >> 16);
    }

    // stts: version/flags(4) entry_count(4) then (sample_count(4), sample_delta(4)) per entry.
    private static void ReadSampleCount(Stream stream, long bodyLength, TrackInfo track)
    {
        Span<byte> buf = stackalloc byte[8];
        if (stream.Read(buf) != 8) return;
        var entries = BinaryPrimitives.ReadUInt32BigEndian(buf[4..8]);
        if (entries > (bodyLength - 8) / 8) return;
        ulong total = 0;
        for (var i = 0; i < entries; i++)
        {
            if (stream.Read(buf) != 8) return;
            total += BinaryPrimitives.ReadUInt32BigEndian(buf[..4]);
        }
        track.SampleCount = total;
    }
}
