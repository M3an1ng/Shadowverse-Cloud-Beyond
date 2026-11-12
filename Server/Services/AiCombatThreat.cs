using System.Collections.Concurrent;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Maximum face damage from visible basic combat after attack allowances refresh.
// Arbitrary triggers/auras are not simulated. If the bounded solve cannot finish,
// return an upper bound, never an unfinished line that could conceal a threat.
internal static class AiCombatThreat
{
    [Flags] private enum Traits { None=0,Shield=1,Bane=2,Immune=4,Resist=8,Super=16,IgnoreWard=32,NoFace=64,NoFollower=128,Ward=256 }
    private readonly record struct Unit(int Attack,int Health,int Attacks,Traits Flags);
    internal sealed record Estimate(int Damage,bool Complete,int States);
    private const int StateBudget=768;
    private readonly record struct StateKey(Unit A0,Unit A1,Unit A2,Unit A3,Unit A4,Unit D0,Unit D1,Unit D2,Unit D3,Unit D4,bool Shield);
    private static readonly ConcurrentDictionary<StateKey,Estimate> Cache=new();
    private static readonly AsyncLocal<bool> Legacy=new();
    private sealed class Lease(bool previous) : IDisposable {
        private bool disposed;
        public void Dispose(){if(disposed)return;disposed=true;Legacy.Value=previous;}
    }
    internal static IDisposable UseLegacy(bool value){var old=Legacy.Value;Legacy.Value=value;return new Lease(old);}
    private static Traits Read(CardInstanceDto card,bool attacker)
    {
        var traits=Traits.None;
        if(card.Keywords.Contains(CardKeyword.Shield))traits|=Traits.Shield;
        if(card.Keywords.Contains(CardKeyword.Bane))traits|=Traits.Bane;
        if(card.Keywords.Contains(CardKeyword.EffectImmune))traits|=Traits.Immune;
        if(card.Keywords.Contains(CardKeyword.DestroyResist))traits|=Traits.Resist;
        if(!attacker&&card.Keywords.Contains(CardKeyword.Ward))traits|=Traits.Ward;
        if(attacker){
            if(card.IsSuperEvolved)traits|=Traits.Super;
            if(card.Keywords.Contains(CardKeyword.IgnoreWard))traits|=Traits.IgnoreWard;
            if(card.Keywords.Contains(CardKeyword.CannotAttackLeader))traits|=Traits.NoFace;
            if(card.Keywords.Contains(CardKeyword.CannotAttackFollower))traits|=Traits.NoFollower;
        }
        return traits;
    }
    private static bool Has(Unit unit,Traits flag)=>(unit.Flags&flag)!=0;
    private static int Saturate(long amount)=>(int)Math.Clamp(amount,0,int.MaxValue);
    private static StateKey Key(Unit[] attackers,Unit[] wards,bool shield) {
        Span<Unit> a=stackalloc Unit[5];Span<Unit> d=stackalloc Unit[5];a.Clear();d.Clear();var na=0;var nd=0;
        foreach(var unit in attackers)if(unit.Health>0&&unit.Attacks>0)a[na++]=unit.Attacks==1?unit with{Health=1}:unit;
        foreach(var unit in wards)if(unit.Health>0)d[nd++]=unit;
        static int Compare(Unit x,Unit y){var result=x.Attack.CompareTo(y.Attack);if(result!=0)return result;
            result=x.Health.CompareTo(y.Health);if(result!=0)return result;result=x.Attacks.CompareTo(y.Attacks);
            return result!=0?result:((int)x.Flags).CompareTo((int)y.Flags);}
        a[..na].Sort(Compare);d[..nd].Sort(Compare);
        return new(a[0],a[1],a[2],a[3],a[4],d[0],d[1],d[2],d[3],d[4],shield);
    }
    internal static Estimate Evaluate(IReadOnlyList<CardInstanceDto> attackers,IReadOnlyList<CardInstanceDto> defenders,
        CardInstanceDto? leader=null)
    {
        if(Legacy.Value)return new(LegacyDamage(attackers,defenders),false,0);
        var active=attackers.Where(c=>c.Type==CardType.Follower&&c.CurrentHealth>0&&c.MaxAttacksPerTurn>0
            &&!c.Keywords.Contains(CardKeyword.Shackle))
            .Select(c=>new Unit(Math.Max(0,c.CurrentAttack),c.CurrentHealth,c.MaxAttacksPerTurn,Read(c,true))).ToArray();
        var needsOptionalTargets=active.Any(u=>Has(u,Traits.Super)&&!Has(u,Traits.NoFollower)&&(u.Attack==0||Has(u,Traits.NoFace)));
        var wards=defenders.Where(c=>c.Type==CardType.Follower&&c.CurrentHealth>0&&(needsOptionalTargets||c.Keywords.Contains(CardKeyword.Ward))
            &&!c.Keywords.Contains(CardKeyword.Ambush)&&!c.Keywords.Contains(CardKeyword.Pressure))
            .Select(c=>new Unit(Math.Max(0,c.CurrentAttack),c.CurrentHealth,0,Read(c,false))).ToArray();
        var shield=leader?.Keywords.Contains(CardKeyword.Shield)==true;
        int OpenDamage(Unit[] units,bool leaderShield){
            var ready=units.Where(u=>u.Health>0&&u.Attacks>0&&!Has(u,Traits.NoFace)).ToArray();
            var total=ready.Sum(u=>(long)u.Attack*u.Attacks);
            return Saturate(total-(leaderShield&&ready.Length>0?ready.Min(u=>u.Attack):0));
        }
        if(wards.Length==0||!wards.Any(u=>Has(u,Traits.Ward))&&!needsOptionalTargets)return new(OpenDamage(active,shield),true,0);
        var upper=Saturate(active.Where(u=>!Has(u,Traits.NoFace)).Sum(u=>(long)u.Attack*u.Attacks)
            +Math.Min(wards.Length,active.Where(u=>Has(u,Traits.Super)&&!Has(u,Traits.NoFollower)).Sum(u=>(long)u.Attacks)));
        if(active.Length>5||wards.Length>5||active.Sum(u=>(long)u.Attacks)>16)return new(upper,false,0);
        var rootKey=Key(active,wards,shield);
        if(Cache.TryGetValue(rootKey,out var cached))return cached;
        var memo=new Dictionary<StateKey,int>();var states=0;var exhausted=false;
        int Solve(Unit[] units,Unit[] blockers,bool leaderShield)
        {
            var live=blockers.Where(u=>u.Health>0).ToArray();
            var liveWards=live.Where(u=>Has(u,Traits.Ward)).ToArray();
            var ready=units.Where(u=>u.Health>0&&u.Attacks>0).ToArray();
            if(liveWards.Length==0&&!ready.Any(u=>Has(u,Traits.Super)&&!Has(u,Traits.NoFollower)&&(u.Attack==0||Has(u,Traits.NoFace))))
                return OpenDamage(units,leaderShield);
            if(live.Length==0)return OpenDamage(units,leaderShield);
            if(exhausted)return 0;
            var key=Key(units,blockers,leaderShield);
            if(memo.TryGetValue(key,out var known))return known;
            if(++states>StateBudget){exhausted=true;return 0;}
            if(!ready.Any(u=>Has(u,Traits.Super)&&!Has(u,Traits.NoFollower))) {
                var clearers=ready.Where(u=>!Has(u,Traits.NoFollower)).ToArray();
                var attacks=clearers.Sum(u=>(long)u.Attacks);var baneUses=clearers.Where(u=>Has(u,Traits.Bane)).Sum(u=>(long)u.Attacks);
                var shieldExtra=Math.Max(0,liveWards.Count(u=>Has(u,Traits.Shield))-baneUses);
                var damageBudget=clearers.Sum(u=>(long)u.Attack*u.Attacks);
                var generousBaneCredit=liveWards.Where(u=>!Has(u,Traits.Immune)&&!Has(u,Traits.Resist)).OrderByDescending(u=>u.Health)
                    .Take((int)Math.Min(baneUses,liveWards.Length)).Sum(u=>(long)u.Health);
                if(attacks<liveWards.Length+shieldExtra||damageBudget+generousBaneCredit<liveWards.Sum(u=>(long)u.Health))
                    return memo[key]=OpenDamage(ready.Where(u=>Has(u,Traits.IgnoreWard)).ToArray(),leaderShield);
            }
            var best=0;var seenUnits=new HashSet<Unit>();
            for(var i=0;i<units.Length;i++){
                var source=units[i];if(source.Health<=0||source.Attacks<=0||!seenUnits.Add(source))continue;
                if(!Has(source,Traits.NoFace)&&(liveWards.Length==0||Has(source,Traits.IgnoreWard))){
                    var next=(Unit[])units.Clone();next[i]=source with{Attacks=source.Attacks-1};
                    best=Math.Max(best,Saturate((leaderShield?0:source.Attack)+(long)Solve(next,blockers,false)));
                }
                if(Has(source,Traits.NoFollower))continue;
                var seenWards=new HashSet<Unit>();
                for(var j=0;j<blockers.Length;j++){
                    var target=blockers[j];if(target.Health<=0||!seenWards.Add(target))continue;
                    if(liveWards.Length>0&&!Has(source,Traits.IgnoreWard)&&!Has(target,Traits.Ward))continue;
                    if(!Has(target,Traits.Ward)&&(!Has(source,Traits.Super)||source.Attack>0&&!Has(source,Traits.NoFace)))continue;
                    if(source.Attack==0&&!Has(source,Traits.Bane)&&!Has(target,Traits.Shield))continue;
                    var targetHealth=target.Health-(Has(target,Traits.Shield)?0:source.Attack);
                    var sourceHealth=source.Health-(Has(source,Traits.Shield)||Has(source,Traits.Super)?0:target.Attack);
                    var targetDies=targetHealth<=0||Has(source,Traits.Bane)&&!Has(target,Traits.Resist)&&!Has(target,Traits.Immune);
                    var sourceDies=sourceHealth<=0||Has(target,Traits.Bane)&&!Has(source,Traits.Resist)&&!Has(source,Traits.Immune);
                    var next=(Unit[])units.Clone();var remaining=(Unit[])blockers.Clone();
                    next[i]=source with{Attacks=source.Attacks-1,Health=sourceDies?0:sourceHealth,Flags=source.Flags&~Traits.Shield};
                    remaining[j]=target with{Health=targetDies?0:targetHealth,Flags=target.Flags&~Traits.Shield};
                    var ping=targetDies&&Has(source,Traits.Super);
                    best=Math.Max(best,Saturate((ping&&!leaderShield?1L:0)+Solve(next,remaining,ping?false:leaderShield)));
                }
            }
            memo[key]=best;return best;
        }
        var damage=Solve(active,wards,shield);
        var result=new Estimate(exhausted?upper:damage,!exhausted,states);
        if(Cache.Count>=2048)Cache.Clear();Cache.TryAdd(rootKey,result);return result;
    }
    // Frozen pre-fix aggregate estimate, restricted to explicit local policy comparisons.
    private static int LegacyDamage(IReadOnlyList<CardInstanceDto> attackers,IReadOnlyList<CardInstanceDto> defenders)
    {
        var face=0;var bypass=0;var freeClearDamage=0;var cheapestClear=int.MaxValue;
        foreach(var card in attackers){
            if(card.Type!=CardType.Follower||card.CurrentAttack<=0||card.MaxAttacksPerTurn<=0||card.Keywords.Contains(CardKeyword.Shackle))continue;
            var damage=card.CurrentAttack*card.MaxAttacksPerTurn;var canHitFace=!card.Keywords.Contains(CardKeyword.CannotAttackLeader);
            if(canHitFace){face+=damage;if(card.Keywords.Contains(CardKeyword.IgnoreWard))bypass+=damage;}
            if(card.Keywords.Contains(CardKeyword.CannotAttackFollower))continue;
            if(canHitFace)cheapestClear=Math.Min(cheapestClear,card.CurrentAttack);else freeClearDamage+=damage;
        }
        if(face==0)return 0;var wardHealth=0;var shieldCount=0;
        foreach(var card in defenders){
            if(card.Type!=CardType.Follower||!card.Keywords.Contains(CardKeyword.Ward)||card.Keywords.Contains(CardKeyword.Ambush)||card.Keywords.Contains(CardKeyword.Pressure))continue;
            wardHealth+=Math.Max(1,card.CurrentHealth);if(card.Keywords.Contains(CardKeyword.Shield))shieldCount++;
        }
        if(wardHealth==0||freeClearDamage>=wardHealth&&shieldCount==0)return face;
        if(cheapestClear==int.MaxValue)return bypass;
        return Math.Max(bypass,face-Math.Max(cheapestClear,wardHealth-freeClearDamage));
    }
}
