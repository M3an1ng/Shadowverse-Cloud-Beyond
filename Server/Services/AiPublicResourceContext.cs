using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Only the actor's public blood counter and both public emblem registries; no hidden cards or identities.
internal static class AiPublicResourceContext
{
    internal const int EmblemLimit=32;
    private sealed record Emblems(int[]? Ids,int Omitted);
    private static Emblems Capture(IEnumerable<(int Id,Guid Owner,CardZone Zone)> cards,Guid owner)
    {
        var rows=cards.ToArray();
        if(rows.Any(c=>c.Id<=0||c.Owner!=owner||c.Zone!=CardZone.Emblem))return new(null,0);
        return new(rows.Select(c=>c.Id).Order().Take(EmblemLimit).ToArray(),Math.Max(0,rows.Length-EmblemLimit));
    }
    private static AiObservedResources Apply(AiObservedResources before,int blood,Emblems own,Emblems enemy)=>before with {
        BloodPool=blood>=0?blood:null,OwnEmblemIds=own.Ids,OwnEmblemsOmitted=own.Omitted,
        EnemyEmblemIds=enemy.Ids,EnemyEmblemsOmitted=enemy.Omitted
    };
    internal static AiObservedResources Capture(AiObservedResources before,PlayerStateDto own,PlayerStateDto enemy)=>Apply(before,own.BloodPool,
        Capture(own.Emblem.Select(c=>(c.CardId,c.OwnerPlayerId,c.Zone)),own.PlayerId),
        Capture(enemy.Emblem.Select(c=>(c.CardId,c.OwnerPlayerId,c.Zone)),enemy.PlayerId));
    internal static AiObservedResources Capture(AiObservedResources before,PlayerState own,PlayerState enemy)=>Apply(before,own.BloodPool,
        Capture(own.Emblem.Select(c=>(c.CurrentDefinitionId,c.OwnerPlayerId,c.CurrentZone)),own.PlayerId),
        Capture(enemy.Emblem.Select(c=>(c.CurrentDefinitionId,c.OwnerPlayerId,c.CurrentZone)),enemy.PlayerId));
    internal static int[]? CurrentEmblems(PlayerStateDto player)
    {
        var result=Capture(player.Emblem.Select(c=>(c.CardId,c.OwnerPlayerId,c.Zone)),player.PlayerId);
        return result.Omitted==0?result.Ids:null;
    }
}
