using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadStandaloneAnalysis;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public sealed class UploadStandaloneAnalysisTests
{
    [Fact]
    public async Task Handle_PngUpload_ShouldExtractWidthAndHeightMetadata_AndPreserveStreamForStorage()
    {
        var userId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var pngBytes = CreateMinimalPngHeader(1920, 1080);

        var missionRepo = new Mock<IGenericRepository<Mission>>();
        missionRepo
            .Setup(r => r.GetByIdAsync(missionId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mission { Id = missionId, Title = "Test Mission" });

        var towerRepo = new Mock<IGenericRepository<Tower>>();
        var mediaRepo = new Mock<IGenericRepository<InspectionMedia>>();
        InspectionMedia? savedMedia = null;
        mediaRepo
            .Setup(r => r.AddAsync(It.IsAny<InspectionMedia>()))
            .ReturnsAsync((InspectionMedia m) =>
            {
                savedMedia = m;
                return m;
            });

        var runRepo = new Mock<IGenericRepository<AIAnalysisRun>>();
        runRepo
            .Setup(r => r.AddAsync(It.IsAny<AIAnalysisRun>()))
            .ReturnsAsync((AIAnalysisRun r) => r);

        var fileStorage = new Mock<IFileStorageService>();
        long? streamPosAtSave = null;
        fileStorage
            .Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((stream, _) => streamPosAtSave = stream.Position)
            .ReturnsAsync("https://storage.local/test.png");

        var currentUser = new Mock<ICurrentUserServices>();
        currentUser.SetupGet(u => u.UserId).Returns(userId);
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);

        var publisher = new Mock<IEventPublisher>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new UploadStandaloneAnalysisCommandHandler(
            missionRepo.Object,
            towerRepo.Object,
            mediaRepo.Object,
            runRepo.Object,
            fileStorage.Object,
            publisher.Object,
            currentUser.Object,
            unitOfWork.Object,
            Mock.Of<ILogger<UploadStandaloneAnalysisCommandHandler>>());

        await using var stream = new MemoryStream(pngBytes);
        var command = new UploadStandaloneAnalysisCommand
        {
            MissionId = missionId,
            Files =
            [
                new UploadedFileInput
                {
                    Stream = stream,
                    FileName = "tower_shot.png",
                    ContentType = "image/png",
                    Length = pngBytes.Length
                }
            ]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        savedMedia.Should().NotBeNull();
        savedMedia!.WidthPx.Should().Be(1920);
        savedMedia.HeightPx.Should().Be(1080);
        savedMedia.DurationSeconds.Should().BeNull();
        savedMedia.Fps.Should().BeNull();
        streamPosAtSave.Should().Be(0);
    }

    private static byte[] CreateMinimalPngHeader(int width, int height)
    {
        var bytes = new byte[24];
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;
        // IHDR chunk length (13) + "IHDR"
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }
}
