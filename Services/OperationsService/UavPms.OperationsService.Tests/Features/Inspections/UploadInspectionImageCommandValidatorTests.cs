using System;
using System.IO;
using FluentAssertions;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class UploadInspectionImageCommandValidatorTests
{
    private readonly UploadInspectionImageCommandValidator _validator;

    public UploadInspectionImageCommandValidatorTests()
    {
        _validator = new UploadInspectionImageCommandValidator();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenMissionIdIsEmpty()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.Empty,
            AssetId = Guid.NewGuid(),
            FileName = "test.jpg",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MissionId");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenAssetIdIsEmpty()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.Empty,
            FileName = "test.jpg",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssetId");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenFileStreamIsNull()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            FileName = "test.jpg",
            FileStream = null!,
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileStream");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenContentTypeIsInvalid()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            FileName = "document.pdf",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "application/pdf"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentType");
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenRequestIsValid()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            FileName = "test.jpg",
            FileStream = ValidJpeg(),
            ContentType = "image/jpeg",
            CapturedAt = DateTime.UtcNow
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRejectSpoofedMediaSignature()
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            FileName = "spoofed.jpg",
            FileStream = new MemoryStream("not-an-image"u8.ToArray()),
            ContentType = "image/jpeg",
            CapturedAt = DateTime.UtcNow
        };

        _validator.Validate(command).Errors.Should()
            .Contain(error => error.ErrorMessage.Contains("signature"));
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void Validate_ShouldRejectInvalidGps(double latitude, double longitude)
    {
        var command = new UploadInspectionImageCommand
        {
            MissionId = Guid.NewGuid(), AssetId = Guid.NewGuid(), FileName = "test.jpg",
            FileStream = ValidJpeg(), ContentType = "image/jpeg", CapturedAt = DateTime.UtcNow,
            Latitude = latitude, Longitude = longitude
        };

        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    private static MemoryStream ValidJpeg() =>
        new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0xFF, 0xD9 });
}
