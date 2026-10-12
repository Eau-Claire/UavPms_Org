using System;
using System.IO;
using FluentAssertions;
using UavPms.OperationsService.Application.Common.Utilities;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class ExifMetadataExtractorTests
{
    [Fact]
    public void Extract_ShouldReturnNulls_WhenStreamIsNull()
    {
        var result = ExifMetadataExtractor.Extract(null);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldReturnNulls_WhenStreamTooShort()
    {
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0x00 });
        var result = ExifMetadataExtractor.Extract(stream);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldReturnNulls_WhenNotJpeg()
    {
        using var stream = new MemoryStream(new byte[32]);
        var result = ExifMetadataExtractor.Extract(stream);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldReturnNulls_WhenJpegHasNoApp1()
    {
        // JPEG SOI followed by DQT (FF DB) and EOI (FF D9)
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x04, 0x01, 0x02, 0xFF, 0xD9 });
        var result = ExifMetadataExtractor.Extract(stream);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldParseGpsAndDateTime_WhenValidExifApp1Present()
    {
        // Build a synthetic JPEG with Exif APP1 containing TIFF Little-Endian payload
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xD8 }); // SOI

        // APP1 Marker
        ms.WriteByte(0xFF);
        ms.WriteByte(0xE1);

        // We will construct TIFF bytes:
        using var tiff = new MemoryStream();
        // Exif\0\0 header: 6 bytes
        tiff.Write(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00 });

        // TIFF Little-Endian Header: 8 bytes
        tiff.Write(new byte[] { 0x49, 0x49, 0x2A, 0x00 }); // II, 42
        tiff.Write(BitConverter.GetBytes((uint)8)); // IFD0 offset = 8

        // IFD0: 2 entries: DateTime (0x0132) and GpsIFD (0x8825)
        // Offset relative to TIFF header start (which is at index 6 of tiff stream)
        // Let's compute offsets:
        // TIFF header: 8 bytes (0..7)
        // IFD0 count: 2 bytes (8..9)
        // Entry 1 (DateTime): 12 bytes (10..21)
        // Entry 2 (GpsIFD): 12 bytes (22..33)
        // Next IFD offset: 4 bytes (34..37) -> 0
        // Data area starts at offset 38:
        // DateTime string: "2026:10:02 10:30:00\0" = 20 bytes (38..57)
        // GPS IFD starts at offset 58:
        // GPS IFD count: 2 bytes (58..59)
        // GPS Entry 1 (LatitudeRef): 12 bytes (60..71)
        // GPS Entry 2 (Latitude): 12 bytes (72..83)
        // GPS Entry 3 (LongitudeRef): 12 bytes (84..95)
        // GPS Entry 4 (Longitude): 12 bytes (96..107)
        // Next GPS IFD: 4 bytes (108..111) -> 0
        // GPS Rationals start at 112:
        // Lat: 10/1, 45/1, 30/1 -> 3 * 8 = 24 bytes (112..135)
        // Lon: 106/1, 40/1, 15/1 -> 3 * 8 = 24 bytes (136..159)

        tiff.Write(BitConverter.GetBytes((ushort)2)); // IFD0 has 2 tags

        // Tag 1: 0x0132, Type 2 (ASCII), count 20, value offset 38
        tiff.Write(BitConverter.GetBytes((ushort)0x0132));
        tiff.Write(BitConverter.GetBytes((ushort)2));
        tiff.Write(BitConverter.GetBytes((uint)20));
        tiff.Write(BitConverter.GetBytes((uint)38));

        // Tag 2: 0x8825, Type 4 (LONG), count 1, value 58 (GPS IFD offset)
        tiff.Write(BitConverter.GetBytes((ushort)0x8825));
        tiff.Write(BitConverter.GetBytes((ushort)4));
        tiff.Write(BitConverter.GetBytes((uint)1));
        tiff.Write(BitConverter.GetBytes((uint)58));

        tiff.Write(new byte[] { 0, 0, 0, 0 }); // Next IFD offset = 0

        // Offset 38: DateTime string
        var dateStrBytes = System.Text.Encoding.ASCII.GetBytes("2026:10:02 10:30:00\0");
        tiff.Write(dateStrBytes);

        // Offset 58: GPS IFD
        tiff.Write(BitConverter.GetBytes((ushort)4)); // 4 GPS tags

        // GPS Tag 1: 0x0001 (LatitudeRef), Type 2, count 2, value 'N'
        tiff.Write(BitConverter.GetBytes((ushort)0x0001));
        tiff.Write(BitConverter.GetBytes((ushort)2));
        tiff.Write(BitConverter.GetBytes((uint)2));
        tiff.Write(new byte[] { (byte)'N', 0, 0, 0 });

        // GPS Tag 2: 0x0002 (Latitude), Type 5, count 3, offset 112
        tiff.Write(BitConverter.GetBytes((ushort)0x0002));
        tiff.Write(BitConverter.GetBytes((ushort)5));
        tiff.Write(BitConverter.GetBytes((uint)3));
        tiff.Write(BitConverter.GetBytes((uint)112));

        // GPS Tag 3: 0x0003 (LongitudeRef), Type 2, count 2, value 'E'
        tiff.Write(BitConverter.GetBytes((ushort)0x0003));
        tiff.Write(BitConverter.GetBytes((ushort)2));
        tiff.Write(BitConverter.GetBytes((uint)2));
        tiff.Write(new byte[] { (byte)'E', 0, 0, 0 });

        // GPS Tag 4: 0x0004 (Longitude), Type 5, count 3, offset 136
        tiff.Write(BitConverter.GetBytes((ushort)0x0004));
        tiff.Write(BitConverter.GetBytes((ushort)5));
        tiff.Write(BitConverter.GetBytes((uint)3));
        tiff.Write(BitConverter.GetBytes((uint)136));

        tiff.Write(new byte[] { 0, 0, 0, 0 }); // Next GPS IFD = 0

        // Offset 112: Lat Rationals (10/1 deg, 45/1 min, 30/1 sec)
        // 10 + 45/60 + 30/3600 = 10 + 0.75 + 0.0083333 = 10.7583333
        WriteRational(tiff, 10, 1);
        WriteRational(tiff, 45, 1);
        WriteRational(tiff, 30, 1);

        // Offset 136: Lon Rationals (106/1 deg, 40/1 min, 15/1 sec)
        // 106 + 40/60 + 15/3600 = 106 + 0.6666667 + 0.0041667 = 106.6708333
        WriteRational(tiff, 106, 1);
        WriteRational(tiff, 40, 1);
        WriteRational(tiff, 15, 1);

        var tiffPayload = tiff.ToArray();
        // APP1 length = tiffPayload.Length + 2
        var segLen = (ushort)(tiffPayload.Length + 2);
        ms.WriteByte((byte)(segLen >> 8));
        ms.WriteByte((byte)(segLen & 0xFF));
        ms.Write(tiffPayload);

        // End with EOI
        ms.Write(new byte[] { 0xFF, 0xD9 });

        ms.Position = 0;
        var result = ExifMetadataExtractor.Extract(ms);

        result.Latitude.Should().NotBeNull();
        result.Latitude!.Value.Should().BeApproximately(10.7583333, 0.0001);

        result.Longitude.Should().NotBeNull();
        result.Longitude!.Value.Should().BeApproximately(106.6708333, 0.0001);

        result.CapturedAt.Should().NotBeNull();
        result.CapturedAt!.Value.Should().Be(new DateTime(2026, 10, 2, 10, 30, 0, DateTimeKind.Utc));
    }

    private static void WriteRational(Stream s, uint num, uint den)
    {
        s.Write(BitConverter.GetBytes(num));
        s.Write(BitConverter.GetBytes(den));
    }
}
