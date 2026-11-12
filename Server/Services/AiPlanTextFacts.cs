using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ShadowverseBeyond.Server.Services;

// Model explanations remain archived, but are not automatically inherited as playing knowledge.
internal static class AiPlanTextFacts
{
    internal sealed record Coverage(bool Known,int? Recorded,int Included=0,
        string Status="unverified-model-prose-withheld-from-live-planning",bool ArchivedForReview=true);
    internal sealed record Text(int Index,string Value);
    internal sealed record Archive(string Value, AiStrategyPlanner.PlanTextCapture Capture);
    internal const int ArchiveCharacterLimit = 16384;

    // Keep the accepted explanation separate from the short plan reused in live context.
    // Counts use .NET/JavaScript UTF-16 units, and clipping never splits a surrogate pair.
    internal static Archive ForArchive(int index, int turn, string focus, string explanation)
    {
        var full = $"T{turn} {focus}: {explanation}";
        var length = Math.Min(full.Length, ArchiveCharacterLimit);
        if (length < full.Length && length > 0
            && char.IsHighSurrogate(full[length - 1]) && char.IsLowSurrogate(full[length]))
            length--;
        return new(full[..length], new(index, full.Length, length, length < full.Length));
    }
    internal sealed record Review(Text[] Texts,int? Recorded,int Included,int Omitted,int ContentBytes,int ByteLimit,
        string Status="Unverified model explanations to check against committed facts and current rules; not observations or strategy evidence.");
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
    internal static Coverage ForLive(string[]? plans)=>new(plans!=null,plans?.Length);
    internal static Review ForReview(string[]? plans,int byteLimit=8192)
    {
        if(byteLimit<0)throw new ArgumentOutOfRangeException(nameof(byteLimit));
        var all=plans??[];var selected=new List<Text>();
        var indices=all.Length<=8?Enumerable.Range(0,all.Length):Enumerable.Range(0,4).Concat(Enumerable.Range(all.Length-4,4));
        var bytes=0;
        foreach(var index in indices) {
            if(all[index]==null)continue;
            var candidate=selected.Append(new Text(index,all[index])).ToArray();
            var size=JsonSerializer.SerializeToUtf8Bytes(candidate,Json).Length;
            if(size>byteLimit)continue;
            selected.Add(candidate[^1]);bytes=size;
        }
        return new(selected.ToArray(),plans?.Length,selected.Count,all.Length-selected.Count,bytes,byteLimit);
    }
}
