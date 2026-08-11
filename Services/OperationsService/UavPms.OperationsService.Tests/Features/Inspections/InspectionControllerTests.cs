using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using UavPms.OperationsService.API.Controllers;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class InspectionControllerTests
{
    private readonly Mock<ISender> _mediatorMock;
    private readonly InspectionController _controller;

    public InspectionControllerTests()
    {
        _mediatorMock = new Mock<ISender>();
        _controller = new InspectionController(_mediatorMock.Object);
    }

    [Fact]
    public async Task UploadImage_ShouldDispatchCommandToMediatorAndReturnOk_WhenRequestIsValid()
    {
        var missionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var capturedAt = DateTime.UtcNow;

        var fileMock = new Mock<IFormFile>();
        var content = "dummy image content";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;

        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);

        var commandResult = new UploadInspectionImageResult
        {
            MissionId = missionId,
            FileUrl = "/images/unique_test.jpg",
            MediaType = "Image"
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<UploadInspectionImageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commandResult);

        var result = await _controller.UploadImage(missionId, assetId, capturedAt, fileMock.Object);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Image uploaded successfully.");
        apiResponse.Data.Should().Be(commandResult);

        _mediatorMock.Verify(m => m.Send(It.Is<UploadInspectionImageCommand>(c =>
            c.MissionId == missionId &&
            c.FileName == "test.jpg" &&
            c.ContentType == "image/jpeg"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
