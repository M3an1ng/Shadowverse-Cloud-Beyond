namespace ShadowverseBeyond.Server.Infrastructure.Uploads;

public static class MultipartTempDirectory
{
    public const string EnvironmentVariableName = "ASPNETCORE_TEMP";

    public static string EnsureConfigured(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new ArgumentException("Content root path is required.", nameof(contentRootPath));
        }

        var tempDirectory = Path.Combine(contentRootPath, "App_Data", "aspnetcore-temp");
        Directory.CreateDirectory(tempDirectory);
        Environment.SetEnvironmentVariable(EnvironmentVariableName, tempDirectory);
        return tempDirectory;
    }
}
