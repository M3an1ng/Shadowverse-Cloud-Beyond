using Microsoft.Data.Sqlite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;

var options = ParseArguments(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

var dbPath = ResolvePath(options.DbPath);
var cardsRoot = ResolvePath(options.CardsRoot);

Console.WriteLine($"Database: {dbPath}");
Console.WriteLine($"CardsRoot: {cardsRoot}");
Console.WriteLine($"DryRun: {options.DryRun}");
Console.WriteLine($"BackupDb: {options.BackupDb}");
Console.WriteLine($"OverwriteExisting: {options.OverwriteExisting}");
Console.WriteLine($"DeleteSource: {options.DeleteSource}");
Console.WriteLine($"Quality: {options.Quality}");

if (!options.SkipFileConversion && !Directory.Exists(cardsRoot))
{
    Console.Error.WriteLine($"Cards directory not found: {cardsRoot}");
    return 1;
}

if (!options.SkipDbUpdate && !File.Exists(dbPath))
{
    Console.Error.WriteLine($"Database file not found: {dbPath}");
    return 1;
}

if (!options.DryRun && options.BackupDb && !options.SkipDbUpdate)
{
    var backupPath = $"{dbPath}.bak-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    File.Copy(dbPath, backupPath, overwrite: false);
    Console.WriteLine($"Database backup created: {backupPath}");
}

var conversionSummary = new ConversionSummary();
if (!options.SkipFileConversion)
{
    conversionSummary = await ConvertDetailFilesAsync(cardsRoot, options);
}

var dbSummary = new DbUpdateSummary();
if (!options.SkipDbUpdate)
{
    dbSummary = await UpdateDbDetailPathsAsync(dbPath, options.DryRun);
}

Console.WriteLine();
Console.WriteLine("=== Summary ===");
if (!options.SkipFileConversion)
{
    Console.WriteLine($"Converted: {conversionSummary.Converted}");
    Console.WriteLine($"SkippedExisting: {conversionSummary.SkippedExisting}");
    Console.WriteLine($"SkippedAlreadyWebp: {conversionSummary.SkippedAlreadyWebp}");
    Console.WriteLine($"Failed: {conversionSummary.Failed}");
}
if (!options.SkipDbUpdate)
{
    Console.WriteLine($"DbRowsScanned: {dbSummary.Scanned}");
    Console.WriteLine($"DbRowsUpdated: {dbSummary.Updated}");
    Console.WriteLine($"DbRowsAlreadyWebp: {dbSummary.AlreadyWebp}");
}

if (conversionSummary.Failed > 0)
{
    return 2;
}

return 0;

static async Task<ConversionSummary> ConvertDetailFilesAsync(string cardsRoot, MigrationOptions options)
{
    var summary = new ConversionSummary();
    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    var files = Directory
        .EnumerateFiles(cardsRoot, "*.*", SearchOption.AllDirectories)
        .Where(path =>
        {
            var baseName = Path.GetFileNameWithoutExtension(path);
            return baseName.StartsWith("detail", StringComparison.OrdinalIgnoreCase);
        })
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    foreach (var sourcePath in files)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!allowedExtensions.Contains(extension))
        {
            continue;
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            summary.SkippedAlreadyWebp++;
            continue;
        }

        var targetPath = Path.ChangeExtension(sourcePath, ".webp");
        if (File.Exists(targetPath) && !options.OverwriteExisting)
        {
            summary.SkippedExisting++;
            continue;
        }

        try
        {
            if (!options.DryRun)
            {
                await using (var input = File.OpenRead(sourcePath))
                using (var image = await Image.LoadAsync(input))
                {
                    var encoder = new WebpEncoder { Quality = options.Quality };
                    await image.SaveAsWebpAsync(targetPath, encoder);
                }
                if (options.DeleteSource && !sourcePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(sourcePath);
                }
            }
            summary.Converted++;
        }
        catch (UnknownImageFormatException ex)
        {
            summary.Failed++;
            Console.Error.WriteLine($"[ConvertFailed] Unknown format: {sourcePath} ({ex.Message})");
        }
        catch (Exception ex)
        {
            summary.Failed++;
            Console.Error.WriteLine($"[ConvertFailed] {sourcePath} ({ex.Message})");
        }
    }

    return summary;
}

static async Task<DbUpdateSummary> UpdateDbDetailPathsAsync(string dbPath, bool dryRun)
{
    var summary = new DbUpdateSummary();
    var builder = new SqliteConnectionStringBuilder
    {
        DataSource = dbPath,
        Mode = SqliteOpenMode.ReadWrite
    };

    await using var connection = new SqliteConnection(builder.ToString());
    await connection.OpenAsync();

    var items = new List<(int Id, string Current, string Next)>();
    await using (var select = connection.CreateCommand())
    {
        select.CommandText = @"
SELECT Id, ArtDetail
FROM CardDefinitions
WHERE ArtDetail IS NOT NULL AND TRIM(ArtDetail) <> '';
";
        await using var reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var current = reader.GetString(1);
            var next = ChangeExtensionToWebp(current);
            summary.Scanned++;
            if (string.Equals(current, next, StringComparison.Ordinal))
            {
                summary.AlreadyWebp++;
                continue;
            }
            items.Add((id, current, next));
        }
    }

    if (dryRun || items.Count == 0)
    {
        summary.Updated = items.Count;
        return summary;
    }

    await using var tx = connection.BeginTransaction();
    await using var update = connection.CreateCommand();
    update.CommandText = "UPDATE CardDefinitions SET ArtDetail = $detail WHERE Id = $id;";
    var detailParam = update.CreateParameter();
    detailParam.ParameterName = "$detail";
    update.Parameters.Add(detailParam);
    var idParam = update.CreateParameter();
    idParam.ParameterName = "$id";
    update.Parameters.Add(idParam);
    update.Transaction = tx;

    foreach (var item in items)
    {
        detailParam.Value = item.Next;
        idParam.Value = item.Id;
        await update.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();
    summary.Updated = items.Count;
    return summary;
}

static string ChangeExtensionToWebp(string input)
{
    var trimmed = input.Trim();
    if (trimmed.Length == 0)
    {
        return trimmed;
    }

    var splitIndex = trimmed.IndexOfAny(['?', '#']);
    var pathPart = splitIndex >= 0 ? trimmed[..splitIndex] : trimmed;
    var suffix = splitIndex >= 0 ? trimmed[splitIndex..] : string.Empty;

    if (pathPart.EndsWith("/", StringComparison.Ordinal))
    {
        return trimmed;
    }

    var extension = Path.GetExtension(pathPart);
    if (string.IsNullOrWhiteSpace(extension))
    {
        return $"{pathPart}.webp{suffix}";
    }

    if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
    {
        return trimmed;
    }

    var basePath = pathPart[..^extension.Length];
    return $"{basePath}.webp{suffix}";
}

static string ResolvePath(string path)
{
    return Path.IsPathRooted(path)
        ? path
        : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
}

static MigrationOptions ParseArguments(string[] args)
{
    var options = new MigrationOptions();
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--db":
                options.DbPath = GetNextValue(args, ref i, "--db");
                break;
            case "--cards-root":
                options.CardsRoot = GetNextValue(args, ref i, "--cards-root");
                break;
            case "--quality":
                if (!int.TryParse(GetNextValue(args, ref i, "--quality"), out var quality) || quality < 0 || quality > 100)
                {
                    throw new ArgumentException("Invalid --quality value. Expected integer 0..100.");
                }
                options.Quality = quality;
                break;
            case "--dry-run":
                options.DryRun = true;
                break;
            case "--no-backup":
                options.BackupDb = false;
                break;
            case "--overwrite-existing":
                options.OverwriteExisting = true;
                break;
            case "--delete-source":
                options.DeleteSource = true;
                break;
            case "--skip-db-update":
                options.SkipDbUpdate = true;
                break;
            case "--skip-file-conversion":
                options.SkipFileConversion = true;
                break;
            case "--help":
            case "-h":
                options.ShowHelp = true;
                break;
            default:
                throw new ArgumentException($"Unknown argument: {arg}");
        }
    }

    return options;
}

static string GetNextValue(string[] args, ref int index, string argName)
{
    var nextIndex = index + 1;
    if (nextIndex >= args.Length)
    {
        throw new ArgumentException($"Missing value for {argName}");
    }

    index = nextIndex;
    return args[nextIndex];
}

static void PrintHelp()
{
    Console.WriteLine("Card detail art migration tool");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tools/CardDetailWebpMigrator/CardDetailWebpMigrator.csproj -- [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --db <path>                SQLite database path (default: Server/Data/shadowverse.db)");
    Console.WriteLine("  --cards-root <path>        Cards root path (default: assets/cards)");
    Console.WriteLine("  --quality <0..100>         WebP quality (default: 82)");
    Console.WriteLine("  --dry-run                  Preview actions without writing files/db");
    Console.WriteLine("  --no-backup                Do not create db backup");
    Console.WriteLine("  --overwrite-existing       Overwrite existing .webp files");
    Console.WriteLine("  --delete-source            Delete original non-webp detail files after conversion");
    Console.WriteLine("  --skip-db-update           Convert files only");
    Console.WriteLine("  --skip-file-conversion     Update db only");
}

internal sealed class MigrationOptions
{
    public string DbPath { get; set; } = Path.Combine("Server", "Data", "shadowverse.db");
    public string CardsRoot { get; set; } = Path.Combine("assets", "cards");
    public int Quality { get; set; } = 82;
    public bool DryRun { get; set; }
    public bool BackupDb { get; set; } = true;
    public bool OverwriteExisting { get; set; }
    public bool DeleteSource { get; set; }
    public bool SkipDbUpdate { get; set; }
    public bool SkipFileConversion { get; set; }
    public bool ShowHelp { get; set; }
}

internal sealed class ConversionSummary
{
    public int Converted { get; set; }
    public int SkippedExisting { get; set; }
    public int SkippedAlreadyWebp { get; set; }
    public int Failed { get; set; }
}

internal sealed class DbUpdateSummary
{
    public int Scanned { get; set; }
    public int Updated { get; set; }
    public int AlreadyWebp { get; set; }
}
