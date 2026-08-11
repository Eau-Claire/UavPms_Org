using FluentAssertions;
using UavPms.OperationsService.Application.Common.Utilities;
using Xunit;

namespace UavPms.OperationsService.Tests.Common.Utilities;

public class FileSanitizerTests
{
    [Theory]
    [InlineData("image.jpg", true)]
    [InlineData("photo.PNG", true)]
    [InlineData("video.mp4", true)]
    [InlineData("script.sh", false)]
    [InlineData("malicious.exe", false)]
    [InlineData("", false)]
    public void IsAllowedExtension_ShouldValidateExtensionsCorrectly(string fileName, bool expectedResult)
    {
        // Act
        var result = FileSanitizer.IsAllowedExtension(fileName);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("my photo 2026!.png", "my_photo_2026_.png")]
    [InlineData("normal_file.jpg", "normal_file.jpg")]
    public void SanitizeFileName_ShouldRemovePathTraversalAndUnsafeCharacters(string input, string expectedOutput)
    {
        // Act
        var result = FileSanitizer.SanitizeFileName(input);

        // Assert
        result.Should().Be(expectedOutput);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Assertions", "xUnit2009:Do not use Assert.True() to check for substring specification", Justification = "Testing GUID prefix format")]
    public void GenerateUniqueFileName_ShouldIncludeGuidPrefix()
    {
        // Arrange
        var fileName = "test_image.jpg";

        // Act
        var result = FileSanitizer.GenerateUniqueFileName(fileName);

        // Assert
        result.Should().EndWith("_test_image.jpg");
        result.Length.Should().BeGreaterThan(fileName.Length + 30);
    }
}