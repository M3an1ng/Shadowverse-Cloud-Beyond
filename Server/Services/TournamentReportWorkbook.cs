using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

public static class TournamentReportWorkbook
{
    public static byte[] Write(TournamentReportSource source, TournamentReportDto overall, CancellationToken cancellationToken)
    {
        var phases = source.Stages.Length > 1
            ? source.Stages.ToDictionary(stage => stage.Number, stage => TournamentReportStatistics.Build(source, stage.Number))
            : [];
        var sheets = TournamentReportTemplateSheets.Build(source, overall, phases).ToArray();
        return TournamentXlsx.Write(sheets, cancellationToken);
    }
}
