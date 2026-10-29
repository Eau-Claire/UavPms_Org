using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using UavPms.OperationsService.Application.Common.Utilities;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class MediaMetadataExtractorTests
{
    private static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); return b; }
    private static byte[] U16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, v); return b; }

    private static byte[] Box(string type, params byte[][] body)
    {
        var payload = body.SelectMany(b => b).ToArray();
        return U32((uint)(payload.Length + 8)).Concat(Encoding.ASCII.GetBytes(type)).Concat(payload).ToArray();
    }

    private static byte[] FullBoxV0(params byte[][] body) => new byte[4].Concat(body.SelectMany(b => b)).ToArray();

    /// <summary>MP4 with one 1280x720 video track: 150 frames over 5 s (timescale 30000) and a 5 s movie header.</summary>
    private static byte[] Mp4Video()
    {
        var mvhd = Box("mvhd", FullBoxV0(U32(0), U32(0), U32(1000), U32(5000), new byte[80]));
        var tkhd = Box("tkhd", FullBoxV0(U32(0), U32(0), U32(1), U32(0), U32(5000), new byte[8], new byte[8], new byte[36],
            U32(1280u << 16), U32(720u << 16)));
        var mdhd = Box("mdhd", FullBoxV0(U32(0), U32(0), U32(30000), U32(150000), new byte[4]));
        var hdlr = Box("hdlr", FullBoxV0(U32(0), Encoding.ASCII.GetBytes("vide"), new byte[12], new byte[] { 0 }));
        var stts = Box("stts", FullBoxV0(U32(1), U32(150), U32(1000)));
        var trak = Box("trak", tkhd, Box("mdia", mdhd, hdlr, Box("minf", Box("stbl", stts))));
        var ftyp = Box("ftyp", Encoding.ASCII.GetBytes("isom"), U32(512));
        return ftyp.Concat(Box("mdat", new byte[32])).Concat(Box("moov", mvhd, trak)).ToArray();
    }

    [Fact]
    public void Extract_Mp4_ReadsDurationSizeAndFps_WhenMoovIsAtTheEnd()
    {
        var result = MediaMetadataExtractor.Extract(new MemoryStream(Mp4Video()), "video/mp4");

        result.Should().Be(new MediaMetadata(1280, 720, 5.0, 30.0));
    }

    [Fact]
    public void Extract_Jpeg_ReadsSizeFromStartOfFrame()
    {
        byte[] jpeg =
        [
            0xFF, 0xD8,
            0xFF, 0xE0, .. U16(16), .. new byte[14], // APP0
            0xFF, 0xC0, .. U16(17), 8, .. U16(3000), .. U16(4000), 3, .. new byte[9], // SOF0: height 3000, width 4000
            0xFF, 0xD9
        ];

        MediaMetadataExtractor.Extract(new MemoryStream(jpeg), "image/jpeg").Should().Be(new MediaMetadata(4000, 3000, null, null));
    }

    [Fact]
    public void Extract_WebPExtended_ReadsCanvasSize()
    {
        byte[] webp =
        [
            .. "RIFF"u8, 22, 0, 0, 0, .. "WEBP"u8, .. "VP8X"u8, 10, 0, 0, 0,
            0, 0, 0, 0,
            0x7F, 0x07, 0x00, // 1920 - 1
            0x37, 0x04, 0x00 // 1080 - 1
        ];

        MediaMetadataExtractor.Extract(new MemoryStream(webp), "image/webp").Should().Be(new MediaMetadata(1920, 1080, null, null));
    }

    [Fact]
    public void Extract_TiffLittleEndian_ReadsWidthAndLength()
    {
        byte[] tiff =
        [
            0x49, 0x49, 0x2A, 0x00, 8, 0, 0, 0,
            2, 0,
            0x00, 0x01, 3, 0, 1, 0, 0, 0, 0x40, 0x01, 0, 0, // ImageWidth SHORT 320
            0x01, 0x01, 4, 0, 1, 0, 0, 0, 0xF0, 0x00, 0, 0, // ImageLength LONG 240
            0, 0, 0, 0
        ];

        MediaMetadataExtractor.Extract(new MemoryStream(tiff), "image/tiff").Should().Be(new MediaMetadata(320, 240, null, null));
    }

    [Fact]
    public void Extract_DamagedFile_ReturnsNullsAndRestoresPosition()
    {
        var stream = new MemoryStream(Encoding.ASCII.GetBytes("this is not a video at all, just text"));
        stream.Position = 5;

        MediaMetadataExtractor.Extract(stream, "video/mp4").Should().Be(new MediaMetadata(null, null, null, null));
        MediaMetadataExtractor.Extract(stream, "image/jpeg").Should().Be(new MediaMetadata(null, null, null, null));
        stream.Position.Should().Be(5);
    }

    [Fact]
    public void ComputeSha256_IsLowerHexOfWholeStream_AndRestoresPosition()
    {
        var bytes = Encoding.UTF8.GetBytes("uav-pms");
        var stream = new MemoryStream(bytes) { Position = 3 };

        var checksum = MediaMetadataExtractor.ComputeSha256(stream);

        checksum.Should().Be(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        checksum.Should().HaveLength(64);
        stream.Position.Should().Be(3);
    }
}
