using ShadowverseBeyond.Server.Infrastructure.Uploads;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class MultipartTempDirectoryTests
{
    [Fact]
    public void EnsureConfigured_CreatesStableAppTempDirectoryAndSetsAspNetCoreTemp()
    {
        var previous = Environment.GetEnvironmentVariable(MultipartTempDirectory.EnvironmentVariableName);
        var root = Path.Combine(Path.GetTempPath(), $"svb-upload-temp-{Guid.NewGuid():N}");

        try
        {
            var configuredPath = MultipartTempDirectory.EnsureConfigured(root);

            Assert.True(Directory.Exists(configuredPath));
            Assert.Equal(Path.Combine(root, "App_Data", "aspnetcore-temp"), configuredPath);
            Assert.Equal(configuredPath, Environment.GetEnvironmentVariable(MultipartTempDirectory.EnvironmentVariableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(MultipartTempDirectory.EnvironmentVariableName, previous);

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
