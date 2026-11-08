using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Dtos.Enums;
namespace ShadowverseBeyond.Server.Domain.Events;
[GameEventMetadata("发现选择", "等待玩家选择发现的卡片。", EventType.DiscoverRequest, visibility:EventMetadataVisibility.System)]
public sealed class DiscoverRequestEvent : GameEventBase, ICommittableEvent {
 private readonly PlayerState player; private readonly List<CardInstance> candidates; private readonly bool copy;private readonly Guid abilityId;
 public CardInstance? Selected {get;private set;}
 public DiscoverRequestEvent(PlayerState player,List<CardInstance> cards,bool copy,Guid abilityId):base(EventType.DiscoverRequest) {this.player=player;candidates=cards;this.copy=copy;this.abilityId=abilityId;Source=TargetInfo.ForPlayer(player);}
 public async Task ResolveAsync(GameContext context) {
  if(Selected!=null || candidates.Count==0)return;
  var request=new PlayerChoiceRequest{PendingChoiceId=Guid.NewGuid(),GameId=context.CurrentGameState.GameId,TargetKey="discover",RequestingPlayerId=player.PlayerId,SourceAbilityRuntimeId=abilityId,Message="请选一张卡片加入手牌",MinSelectionCount=1,MaxSelectionCount=1,IsCancellable=false,SelectableTargetIds=candidates.Select(c=>c.InstanceId).ToList(),SelectableCardInstances=candidates.Select(c=>CardInstanceProjection.Create(c,context)).ToList()};
  var result=await context.ServiceProvider.GetRequiredService<IPendingActionService>().WaitForPlayerChoiceAsync(context.CurrentGameState.GameId,request,context.CancellationToken);
  if(result.WasCancelled)return;
  if(result.ChosenTargetIds.Count!=1 || !candidates.Any(c=>c.InstanceId==result.ChosenTargetIds[0]))throw new InvalidOperationException("发现选择不合法");
  Selected=candidates.Single(c=>c.InstanceId==result.ChosenTargetIds[0]);
 }
 public DomainOperationResult Commit(GameContext context) {
  if(Selected==null)return new(){Success=true};
  var factory=context.ServiceProvider.GetRequiredService<ICardFactory>();
  CardInstance card;
  var result=new DomainOperationResult{Success=true};
  if(copy){card=new CardInstance(Guid.NewGuid(),Selected.CurrentDefinitionId,player.PlayerId){CurrentZone=CardZone.Staging};result.GeneratedEvents.Add(new CopyCardEvent(player,Selected,card));}
  else card=factory.CreateAndRegisterNewCard(context.CurrentGameState,Selected.CurrentDefinitionId,player.PlayerId,CardZone.Staging);
  // EffectCompleted.GeneratedCards reads this request's committed targets.
  // Publish the newly created hand card, never a sampled candidate or copy source.
  Target.Clear();
  Target.Add(TargetInfo.ForCard(card));
  result.GeneratedEvents.Add(new AddCardToHandEvent(player,new(){card}));
  result.GeneratedEvents.Add(new DiscoverEvent(player,card));
  return result;
 }
}
[GameEventMetadata("发现", "玩家发现并将选择的卡片加入手牌。", EventType.Discover)]
public sealed class DiscoverEvent : GameEventBase,ICommittableEvent {
 [GameEventParameter("发现玩家","进行发现的玩家。",DslDisplayTypeName="事件源")] public PlayerState? DiscoveringPlayer=>Source?.Player;
 [GameEventParameter("发现卡片","加入手牌的卡片实例。",DslDisplayTypeName="事件目标")] public CardInstance? DiscoveredCard=>Target.FirstOrDefault()?.Card;
 public DiscoverEvent(PlayerState player,CardInstance card):base(EventType.Discover){Source=TargetInfo.ForPlayer(player);Target.Add(TargetInfo.ForCard(card));}
 public DomainOperationResult Commit(GameContext context)=>new(){Success=true};
}
