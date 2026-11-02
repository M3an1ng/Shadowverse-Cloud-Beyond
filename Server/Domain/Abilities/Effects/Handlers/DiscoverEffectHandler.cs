using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Services;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
[EffectHandler(EffectType.Discover)]
public class DiscoverEffectHandler(ILogger<DiscoverEffectHandler> logger) : EffectHandlerBase<DiscoverEffectParams>(logger) {
 public override GameStateActionResult Apply(Effect effect,GameContext context,DiscoverEffectParams parameters) {
  var player=context.CurrentGameState.GetPlayerState(context.SourceCard?.OwnerPlayerId ?? context.RequestingPlayerId);
  if(player==null) return new(){Success=false,ErrorMessage="发现的玩家不存在"};
  List<CardInstance> pool;
  var copy=parameters.Mode==DiscoverMode.Copy;
  if(copy) {
   if(!TransformEffectHandler.TryResolveCopySourceCards(parameters.CopySourceTargets,context,out pool,out var error)) return new(){Success=false,ErrorMessage=error};
   pool=pool.DistinctBy(c=>c.InstanceId).ToList();
  } else pool=parameters.CardDefinitionIds.Where(id=>id>0 && GameDataProvider.Instance.GetCardDefinition(id)!=null).Distinct().Select(id=>new CardInstance(Guid.NewGuid(),id,player.PlayerId){CurrentZone=Dtos.Enums.CardZone.Staging}).ToList();
  var candidates=Sample(pool.Where(c=>IsDiscoverableType(c.CardType)));
  return new(){Success=true,GeneratedEvents=candidates.Count==0?new():new(){new DiscoverRequestEvent(player,candidates,copy,effect.SourceAbility?.RuntimeId ?? Guid.Empty)}};
 }
 public static bool IsDiscoverableType(Dtos.Enums.CardType type) => type is not (Dtos.Enums.CardType.Crest or Dtos.Enums.CardType.Faith);
 public static List<CardInstance> Sample(IEnumerable<CardInstance> pool) {
  var remaining=pool.DistinctBy(c=>c.InstanceId).ToList();
  var groups=remaining.GroupBy(c=>c.CurrentDefinitionId).OrderBy(_=>global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
  var picked=groups.Take(3).Select(g=>g.ElementAt(global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(g.Count()))).ToList();
  var ids=picked.Select(c=>c.InstanceId).ToHashSet();
  if(picked.Count<3)picked.AddRange(remaining.Where(c=>!ids.Contains(c.InstanceId)).OrderBy(_=>global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue)).Take(3-picked.Count));
  return picked.OrderBy(_=>global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
 }
}
