using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.IdentityService.Infrastructure.Services;
using Xunit;

namespace UavPms.IdentityService.Tests.Services;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"uavpms_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testStoragePath);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FileStorage:AlertImagesPath"]).Returns(_testStoragePath);

        var loggerMock = new Mock<ILogger<LocalFileStorageService>>();
        _service = new LocalFileStorageService(configMock.Object, loggerMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, recursive: true);
        }
    }

    [Theory]
    [InlineData("mission image 01.png")]
    [InlineData("my  file   name.jpg")]
    [InlineData("photo (1).jpeg")]
    public async Task SaveImageAsync_ShouldHandleSpacesInFilename(string fileName)
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic bytes

        // Act
        var url = await _service.SaveImageAsync(stream, fileName);

        // Assert
        url.Should().NotContain(" ", "spaces must be sanitized or encoded in the URL");
        url.Should().StartWith("/images/");

        // The file should actually exist on disk
        var files = Directory.GetFiles(_testStoragePath);
        files.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("special&chars=bad.png")]
    [InlineData("file#hash.jpg")]
    [InlineData("name@email.jpeg")]
    public async Task SaveImageAsync_ShouldSanitizeSpecialCharacters(string fileName)
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });

        // Act
        var url = await _service.SaveImageAsync(stream, fileName);

        // Assert
        url.Should().StartWith("/images/");
        // URL should not contain raw special characters
        url.Should().NotContain("&");
        url.Should().NotContain("#");
        url.Should().NotContain("@");
    }

    [Theory]
    [InlineData("../../etc/passwd.png")]
    [InlineData("..\\..\\windows\\system32\\config.jpg")]
    [InlineData("/absolute/path/evil.png")]
    public async Task SaveImageAsync_ShouldPreventPathTraversal(string fileName)
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Act
        var url = await _service.SaveImageAsync(stream, fileName);

        // Assert — URL should be a valid relative path under /images/
        url.Should().StartWith("/images/");

        // The file must be saved inside the storage directory, not elsewhere
        var files = Directory.GetFiles(_testStoragePath);
        files.Should().HaveCount(1);

        // Verify the saved file is genuinely within the storage path
        var savedFilePath = Path.GetFullPath(files[0]);
        var storagePath = Path.GetFullPath(_testStoragePath);
        savedFilePath.Should().StartWith(storagePath, "file must be saved within the storage directory");
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.php")]
    [InlineData("payload.sh")]
    [InlineData("noextension")]
    public async Task SaveImageAsync_ShouldRejectDisallowedExtensions(string fileName)
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x00 });

        // Act
        Func<Task> act = async () => await _service.SaveImageAsync(stream, fileName);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task SaveImageAsync_ShouldReturnUrlEncodedPath()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Act
        var url = await _service.SaveImageAsync(stream, "mission image 01.png");

        // Assert — the URL should be properly encoded (underscores replacing spaces)
        url.Should().StartWith("/images/");
        url.Should().NotContain(" ");
        // The file on disk should match
        var savedFiles = Directory.GetFiles(_testStoragePath);
        savedFiles.Should().HaveCount(1);
        Path.GetFileName(savedFiles[0]).Should().Contain("mission_image_01.png");
    }

    [Fact]
    public async Task DeleteImageAsync_ShouldPreventPathTraversal()
    {
        // Arrange — create a file first
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var url = await _service.SaveImageAsync(stream, "test.png");

        // Create a decoy file outside storage
        var decoyPath = Path.Combine(Path.GetTempPath(), "decoy_should_not_delete.txt");
        await File.WriteAllTextAsync(decoyPath, "important data");

        // Act — try to delete with path traversal
        await _service.DeleteImageAsync("/images/../../" + Path.GetFileName(decoyPath));

        // Assert — decoy file should still exist
        File.Exists(decoyPath).Should().BeTrue("path traversal in deletion should be blocked");

        // Cleanup
        File.Delete(decoyPath);
    }
}
