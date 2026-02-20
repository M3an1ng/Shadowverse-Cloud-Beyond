param(
    [string]$DbPath = "Server/Data/shadowverse.db",
    [string]$CardsRoot = "assets/cards",
    [int]$Quality = 82,
    [switch]$DryRun,
    [switch]$NoBackup,
    [switch]$OverwriteExisting,
    [switch]$DeleteSource,
    [switch]$SkipDbUpdate,
    [switch]$SkipFileConversion
)

$ErrorActionPreference = "Stop"

$toolProject = "tools/CardDetailWebpMigrator/CardDetailWebpMigrator.csproj"
$toolArgs = @(
    "--db", $DbPath,
    "--cards-root", $CardsRoot,
    "--quality", $Quality.ToString()
)

if ($DryRun) { $toolArgs += "--dry-run" }
if ($NoBackup) { $toolArgs += "--no-backup" }
if ($OverwriteExisting) { $toolArgs += "--overwrite-existing" }
if ($DeleteSource) { $toolArgs += "--delete-source" }
if ($SkipDbUpdate) { $toolArgs += "--skip-db-update" }
if ($SkipFileConversion) { $toolArgs += "--skip-file-conversion" }

dotnet run --project $toolProject -- $toolArgs
exit $LASTEXITCODE
