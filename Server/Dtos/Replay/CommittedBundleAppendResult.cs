namespace ShadowverseBeyond.Server.Dtos.Replay
{
    public sealed record CommittedBundleAppendResult(
        long BundleSeq,
        long StartSeq,
        long EndSeq);
}

