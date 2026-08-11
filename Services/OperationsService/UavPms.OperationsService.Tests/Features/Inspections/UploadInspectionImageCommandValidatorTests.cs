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
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "image/jpeg"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
