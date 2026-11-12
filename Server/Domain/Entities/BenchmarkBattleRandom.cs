namespace ShadowverseBeyond.Server.Domain.Entities;

// Only seeded local evaluations have an instance. The game loop serializes action roots.
internal sealed class BenchmarkBattleRandom
{
    private readonly Random actionSeeds;
    private readonly int previewSeed;

    internal BenchmarkBattleRandom(int seed)
    {
        var seeds = new Random(seed);
        actionSeeds = new Random(seeds.Next());
        previewSeed = seeds.Next();
    }

    internal Random BeginAction() => new(actionSeeds.Next());

    // Availability checks are repeatable at the same revision and never advance an action stream.
    internal Random CreatePreview(long revision) => new(unchecked(previewSeed + (int)(revision ^ (revision >> 32))));
}
