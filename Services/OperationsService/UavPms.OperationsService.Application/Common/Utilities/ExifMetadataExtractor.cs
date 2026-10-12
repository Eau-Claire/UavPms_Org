namespace UavPms.OperationsService.Application.Common.Utilities;

using System;
using System.Globalization;
using System.IO;

public record ExifMetadata(double? Latitude, double? Longitude, DateTime? CapturedAt);

/// <summary>
/// Lightweight, zero-dependency EXIF metadata extractor for JPEG and TIFF images.
/// Safely extracts GPS coordinates (latitude, longitude) and capture timestamp.
/// </summary>
public static class ExifMetadataExtractor
{
    public static ExifMetadata Extract(Stream? stream)
    {
        if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < 16)
        {
            return new ExifMetadata(null, null, null);
        }

        var initialPos = stream.Position;
        try
        {
            stream.Position = 0;
            int b1 = stream.ReadByte();
            int b2 = stream.ReadByte();
            if (b1 != 0xFF || b2 != 0xD8)
            {
                return new ExifMetadata(null, null, null);
            }

            while (stream.Position < stream.Length - 4)
            {
                int markerPrefix = stream.ReadByte();
                if (markerPrefix != 0xFF) break;

                int marker = stream.ReadByte();
                while (marker == 0xFF)
                {
                    marker = stream.ReadByte();
                }

                if (marker is 0xD9 or 0xDA or -1) // EOI, SOS, or EOF
                    break;

                int lengthHigh = stream.ReadByte();
                int lengthLow = stream.ReadByte();
                if (lengthHigh == -1 || lengthLow == -1) break;
                int segmentLength = (lengthHigh << 8) | lengthLow;
                if (segmentLength < 2) break;

                int dataLength = segmentLength - 2;
                long segmentDataStart = stream.Position;

                if (marker == 0xE1 && dataLength >= 14) // APP1 Marker
                {
                    byte[] exifHeader = new byte[6];
                    int readExif = stream.Read(exifHeader, 0, 6);
                    if (readExif == 6 &&
                        exifHeader[0] == (byte)'E' && exifHeader[1] == (byte)'x' &&
                        exifHeader[2] == (byte)'i' && exifHeader[3] == (byte)'f' &&
                        exifHeader[4] == 0 && exifHeader[5] == 0)
                    {
                        byte[] tiffBytes = new byte[dataLength - 6];
                        int tiffRead = stream.Read(tiffBytes, 0, tiffBytes.Length);
                        if (tiffRead == tiffBytes.Length)
                        {
                            return ParseTiffPayload(tiffBytes);
                        }
                    }
                }

                stream.Position = segmentDataStart + dataLength;
            }

            return new ExifMetadata(null, null, null);
        }
        catch
        {
            return new ExifMetadata(null, null, null);
        }
        finally
        {
            stream.Position = initialPos;
        }
    }

    private static ExifMetadata ParseTiffPayload(byte[] bytes)
    {
        if (bytes.Length < 8) return new ExifMetadata(null, null, null);

        bool isLittleEndian;
        if (bytes[0] == 0x49 && bytes[1] == 0x49)
            isLittleEndian = true;
        else if (bytes[0] == 0x4D && bytes[1] == 0x4D)
            isLittleEndian = false;
        else
            return new ExifMetadata(null, null, null);

        ushort magic = ReadUInt16(bytes, 2, isLittleEndian);
        if (magic != 42) return new ExifMetadata(null, null, null);

        uint ifd0Offset = ReadUInt32(bytes, 4, isLittleEndian);
        if (ifd0Offset >= bytes.Length) return new ExifMetadata(null, null, null);

        DateTime? capturedAt = null;
        uint? gpsIfdOffset = null;

        ParseIfd0(bytes, (int)ifd0Offset, isLittleEndian, ref capturedAt, ref gpsIfdOffset);

        double? latitude = null;
        double? longitude = null;
        if (gpsIfdOffset.HasValue && gpsIfdOffset.Value < bytes.Length)
        {
            ParseGpsIfd(bytes, (int)gpsIfdOffset.Value, isLittleEndian, ref latitude, ref longitude, ref capturedAt);
        }

        return new ExifMetadata(latitude, longitude, capturedAt);
    }

    private static void ParseIfd0(byte[] bytes, int offset, bool isLittleEndian, ref DateTime? capturedAt, ref uint? gpsIfdOffset)
    {
        if (offset + 2 > bytes.Length) return;
        ushort entryCount = ReadUInt16(bytes, offset, isLittleEndian);
        int current = offset + 2;

        for (int i = 0; i < entryCount && current + 12 <= bytes.Length; i++, current += 12)
        {
            ushort tag = ReadUInt16(bytes, current, isLittleEndian);
            ushort type = ReadUInt16(bytes, current + 2, isLittleEndian);
            uint count = ReadUInt32(bytes, current + 4, isLittleEndian);
            uint valueOrOffset = ReadUInt32(bytes, current + 8, isLittleEndian);

            if (tag == 0x0132 && type == 2) // DateTime tag
            {
                capturedAt = ParseAsciiDateTime(bytes, valueOrOffset, count);
            }
            else if (tag == 0x8825) // GPS Info IFD Pointer
            {
                gpsIfdOffset = valueOrOffset;
            }
        }
    }

    private static void ParseGpsIfd(byte[] bytes, int offset, bool isLittleEndian, ref double? latitude, ref double? longitude, ref DateTime? capturedAt)
    {
        if (offset + 2 > bytes.Length) return;
        ushort entryCount = ReadUInt16(bytes, offset, isLittleEndian);
        int current = offset + 2;

        char? latRef = null;
        char? lonRef = null;
        double[]? latComponents = null;
        double[]? lonComponents = null;

        for (int i = 0; i < entryCount && current + 12 <= bytes.Length; i++, current += 12)
        {
            ushort tag = ReadUInt16(bytes, current, isLittleEndian);
            ushort type = ReadUInt16(bytes, current + 2, isLittleEndian);
            uint count = ReadUInt32(bytes, current + 4, isLittleEndian);
            uint valueOrOffset = ReadUInt32(bytes, current + 8, isLittleEndian);

            if (tag == 0x0001 && type == 2) // GPSLatitudeRef
            {
                char c = (char)bytes[current + 8];
                latRef = char.ToUpperInvariant(c);
            }
            else if (tag == 0x0002 && type == 5 && count == 3) // GPSLatitude
            {
                latComponents = ReadRationals(bytes, (int)valueOrOffset, 3, isLittleEndian);
            }
            else if (tag == 0x0003 && type == 2) // GPSLongitudeRef
            {
                char c = (char)bytes[current + 8];
                lonRef = char.ToUpperInvariant(c);
            }
            else if (tag == 0x0004 && type == 5 && count == 3) // GPSLongitude
            {
                lonComponents = ReadRationals(bytes, (int)valueOrOffset, 3, isLittleEndian);
            }
        }

        if (latComponents != null && latComponents.Length == 3)
        {
            var lat = latComponents[0] + (latComponents[1] / 60.0) + (latComponents[2] / 3600.0);
            if (latRef == 'S') lat = -lat;
            if (lat >= -90 && lat <= 90) latitude = Math.Round(lat, 7);
        }

        if (lonComponents != null && lonComponents.Length == 3)
        {
            var lon = lonComponents[0] + (lonComponents[1] / 60.0) + (lonComponents[2] / 3600.0);
            if (lonRef == 'W') lon = -lon;
            if (lon >= -180 && lon <= 180) longitude = Math.Round(lon, 7);
        }
    }

    private static double[]? ReadRationals(byte[] bytes, int offset, int count, bool isLittleEndian)
    {
        if (offset + (count * 8) > bytes.Length) return null;
        var result = new double[count];
        for (int i = 0; i < count; i++)
        {
            int entryOffset = offset + (i * 8);
            uint num = ReadUInt32(bytes, entryOffset, isLittleEndian);
            uint den = ReadUInt32(bytes, entryOffset + 4, isLittleEndian);
            if (den == 0) return null;
            result[i] = (double)num / den;
        }
        return result;
    }

    private static DateTime? ParseAsciiDateTime(byte[] bytes, uint offset, uint count)
    {
        if (offset + count > bytes.Length) return null;
        try
        {
            string raw = System.Text.Encoding.ASCII.GetString(bytes, (int)offset, (int)count).Trim('\0', ' ', '\r', '\n');
            if (DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt;
            }
        }
        catch { }
        return null;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset, bool isLittleEndian)
    {
        if (offset + 2 > bytes.Length) return 0;
        return isLittleEndian
            ? (ushort)(bytes[offset] | (bytes[offset + 1] << 8))
            : (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static uint ReadUInt32(byte[] bytes, int offset, bool isLittleEndian)
    {
        if (offset + 4 > bytes.Length) return 0;
        return isLittleEndian
            ? (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24))
            : (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);
    }
}
