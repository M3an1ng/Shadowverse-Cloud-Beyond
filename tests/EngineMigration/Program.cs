using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using ShadowverseBeyond.Server.Infrastructure.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

var results = new List<object>();
void Test(string name, Action action) { try { action(); results.Add(new { name, passed = true }); } catch(Exception e) { results.Add(new {name,passed=false,error=e.ToString()}); } }
void Check(bool yes, string reason) { if(!yes) throw new Exception(reason); }
Test("AI searches combat, evolution and Storm routes", AiPolicyTests.Search);
Test("AI scores damage, healing and removal targets", AiPolicyTests.Targets);
Test("AI values Act resources and Fusion materials", AiPolicyTests.Abilities);
Test("startup preserves shared original card art and explicit remote art", () => {
 var root=Path.Combine(Path.GetTempPath(),"sv-art-"+Guid.NewGuid());
 var source=Path.Combine(root,"assets","cards","1");var target=Path.Combine(root,"assets","cards","2");
 Directory.CreateDirectory(source);Directory.CreateDirectory(target);
 try {
  File.WriteAllText(Path.Combine(source,"preview.png"),"original");File.WriteAllText(Path.Combine(target,"preview.png"),"old alternate");
  var resolve=typeof(ShadowverseBeyond.Server.Infrastructure.Data.DataSeeder).GetMethod("ResolveNormalizedArtPath",BindingFlags.NonPublic|BindingFlags.Static)!;
  Check((string?)resolve.Invoke(null,new object?[]{target,2,"preview","/assets/cards/1/preview.png"})=="/assets/cards/1/preview.png","shared art overwritten");
  Check((string?)resolve.Invoke(null,new object?[]{target,2,"preview","https://example.com/art.png"})=="https://example.com/art.png","remote art overwritten");
  Check((string?)resolve.Invoke(null,new object?[]{target,2,"preview","/missing.png"})=="/assets/cards/2/preview.png","legacy fallback lost");
 } finally { Directory.Delete(root,true); }
});
Test("card history records card and ability saves transactionally",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
 db.Database.EnsureCreated();var card=new CardDefinition{Id=900001,Name="历史测试",Description="旧文本",Cost=1};db.CardDefinitions.Add(card);db.SaveChangesAsync().GetAwaiter().GetResult();
 Check(db.CardRevisions.Count()==1,"create history missing");
 card.Description="新文本";card.UpdatedBy="录卡者";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==2,"edit history missing");
 db.CardAbilities.Add(new AbilityDefinition{Id=90000101,CardId=card.Id,Name="测试能力",JsonConfig="{}"});db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==2,"same editor ability save was not merged");
 var latest=db.CardRevisions.OrderByDescending(x=>x.Revision).First();var snapshot=JsonSerializer.Deserialize<CardConfigurationSnapshot>(latest.Snapshot)!;Check(snapshot.Card.Description=="新文本"&&snapshot.Abilities.Count==1,"snapshot incomplete");
 var count=db.CardRevisions.Count();latest.Note="注释";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==count,"annotation created false revision");
});
Test("history merges recent same-editor saves and preserves editor/time boundaries",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var card=new CardDefinition{Id=900009,Name="合并历史",UpdatedBy="编辑甲"};db.Add(card);db.SaveChangesAsync().GetAwaiter().GetResult();
 card.Description="连续保存";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==1,"new-card edits created duplicate history");
 var latest=db.CardRevisions.Single();latest.Note="保留注释";latest.ModifiedAt=DateTime.UtcNow.AddMinutes(-6);db.SaveChangesAsync().GetAwaiter().GetResult();
 card.Description="间隔保存";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==2,"five-minute boundary ignored");
 card.UpdatedBy="编辑乙";card.Description="另一编辑者";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==3,"different editor merged");
 card.Description="回退边界";db.RevisionOperation="restore";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==4,"restore merged into prior edit");
 Check(db.CardRevisions.First().Note=="保留注释","note lost");
});
Test("history API restores complete configuration and protects the current version",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var actor=new User{Id=Guid.NewGuid(),Username="测试编辑者",Email="editor@example.invalid",PasswordHash="test-only",Role=UserRole.Player,IsActive=true};db.Users.Add(actor);
 var card=new CardDefinition{Id=900002,Name="回退测试",Description="原始文本",Cost=1};db.CardDefinitions.Add(card);db.SaveChangesAsync().GetAwaiter().GetResult();var old=db.CardRevisions.Single();
 card.Description="修改后";db.CardAbilities.Add(new AbilityDefinition{Id=90000201,CardId=card.Id,Name="新增能力",JsonConfig="{}"});db.SaveChangesAsync().GetAwaiter().GetResult();
 var gameService=Proxy<IGameService>.Make((method,args)=>method.ReturnType==typeof(Task)?Task.CompletedTask:null);
 var provider=Proxy<IGameDataProvider>.Make((method,args)=>method.Name=="get_DataRevision"?Guid.Empty:null);
 var mapper=Proxy<IMapper>.Make((method,args)=>null);
 var controller=new ShadowverseBeyond.Server.Controllers.CardHistoryController(db,provider,gameService,mapper){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 controller.Request.Headers["X-Editor-UserId"]=actor.Id.ToString();
 var restored=controller.Restore(card.Id,old.Id,new(2),default).GetAwaiter().GetResult();Check(restored is Microsoft.AspNetCore.Mvc.OkObjectResult,"restore failed");
 Check(db.CardDefinitions.Single().Description=="原始文本"&&!db.CardAbilities.Any(),"configuration not restored");Check(db.CardRevisions.Count()==3,"rollback did not create revision");
 var current=db.CardRevisions.OrderByDescending(h=>h.Revision).First();Check(controller.Delete(card.Id,current.Id,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"current version deleted");
 Check(controller.Restore(card.Id,old.Id,new(2),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"stale revision accepted");
});
Test("card styles remain personal and bound cards inherit only appearance",()=>{
 var owner=Guid.NewGuid();var enemy=Guid.NewGuid();var config=new StyleConfiguration(99001,1,new(){new CardStyle("night","夜色",new("新卡名","展示描述","/assets/new.png",null),new(){new StyleBinding(99002,new("新纹章","纹章描述","/assets/crest.png",null))})});
 CardStyleCatalog.Apply(new FeatureDocument{Id="style:99001",Json=JsonSerializer.Serialize(config,CardStyleCatalog.JsonOptions)});
 CardStyleCatalog.Apply(new FeatureDocument{Id=$"style-prefs:{owner}",Json=JsonSerializer.Serialize(new Dictionary<int,string>{{99001,"night"}},CardStyleCatalog.JsonOptions)});
 Check(CardStyleCatalog.Resolve(owner,99001)?.Name=="新卡名","owner selection absent");Check(CardStyleCatalog.Resolve(owner,99002)?.Name=="新纹章","binding missing");Check(CardStyleCatalog.Resolve(enemy,99001)==null,"enemy's own appearance changed");Check(CardStyleCatalog.Parent(99002)==99001,"bound selector not identified");
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using(var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options)){db.Database.EnsureCreated();db.FeatureDocuments.Add(new(){Id=$"style-prefs:{enemy}",Json="{}"});db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.FeatureDocuments.Count()==1,"appearance preferences did not persist");}
});
Test("style API validates permissions, bindings, revisions and per-account persistence",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var editor=new User{Id=Guid.NewGuid(),Username="风格录卡",Email="style-editor@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};var user=new User{Id=Guid.NewGuid(),Username="风格玩家",Email="style-player@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};db.Users.AddRange(editor,user);db.CardDefinitions.AddRange(new CardDefinition{Id=99011,Name="主卡"},new CardDefinition{Id=99012,Name="纹章"});db.SaveChangesAsync().GetAwaiter().GetResult();
 var games=Proxy<IGameService>.Make((m,a)=>m.ReturnType==typeof(Task)?Task.CompletedTask:null);var environment=Proxy<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>.Make((m,a)=>null);
 var controller=new ShadowverseBeyond.Server.Controllers.CardStylesController(db,games,environment){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var config=new StyleConfiguration(99011,0,new(){new CardStyle("sun","白昼",new("昼","效果说明","/assets/sun.png",null),new(){new StyleBinding(99012,new("昼纹章","对应文本",null,null))})});
 user.Role=UserRole.Guest;db.SaveChangesAsync().GetAwaiter().GetResult();controller.Request.Headers["X-Editor-UserId"]=user.Id.ToString();Check(controller.Save(99011,config,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.StatusCodeResult denied&&denied.StatusCode==403,"guest changed shared styles");
 user.Role=UserRole.Player;db.SaveChangesAsync().GetAwaiter().GetResult();controller.Request.Headers["X-Editor-UserId"]=editor.Id.ToString();Check(controller.Save(99011,config,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"editor could not save");Check(controller.Save(99011,config,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"stale style revision accepted");
 controller.Request.Headers["X-Editor-UserId"]=user.Id.ToString();Check(controller.Choose(new(99011,"sun"),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"style choice failed");Check(CardStyleCatalog.Resolve(user.Id,99012)?.Name=="昼纹章","choice binding not applied");Check(CardStyleCatalog.Resolve(editor.Id,99012)==null,"another account changed");Check(controller.Choose(new(99012,"sun"),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"bound card selected directly");
 Check(db.FeatureDocuments.Count(d=>d.Id.StartsWith("style-prefs:"))==1,"preference storage wrong");controller.Choose(new(99011,null),default).GetAwaiter().GetResult();Check(CardStyleCatalog.Resolve(user.Id,99011)==null,"default style could not be restored");
});
Test("Take Two supports multiple rooms and applies current host pool before drafting",()=>{
 var catalog=new List<CardDefinition>();var id=97800;
 foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend})for(var j=0;j<12;j++)catalog.Add(new(){Id=id++,Name="房间测试",CardSet="卡包",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true,Rarity=rarity});
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var owner=Guid.NewGuid();
 var first=service.Create(owner,"open",TakeTwoService.EmptyPool,catalog);
 var second=service.Create(owner,"open",TakeTwoService.EmptyPool,catalog);
 Check(first.Id!=second.Id&&!first.Closed,"multiple rooms were blocked or old room closed");
 var guest=Guid.NewGuid();service.Join(first.Code,guest);service.Join(second.Code,guest);
 var pool=TakeTwoService.EmptyPool with{Redraws=new(){{"Forestcraft",1}}};
 service.UpdatePool(first,first.Players[0],"open",pool,catalog);
 Check(first.Players.All(p=>p.Plans["Forestcraft"].Redraws==1),"current pool not applied to both seats");
 Check(second.Players.All(p=>p.Plans["Forestcraft"].Redraws==2),"another room changed");
 var denied=false;try{service.UpdatePool(first,first.Players[1],"open",pool,catalog);}catch(UnauthorizedAccessException){denied=true;}
 Check(denied,"guest changed host pool");
 service.Act(first,first.Players[0],"class",1,"Forestcraft",0);
 var locked=false;try{service.UpdatePool(first,first.Players[0],"open",pool,catalog);}catch(InvalidOperationException){locked=true;}
 Check(locked,"pool changed after picks began");
 service.Act(first,first.Players[0],"leave",999,null,0);service.Act(first,first.Players[0],"leave",0,null,0);
 Check(first.Closed&&!second.Closed,"leave was not idempotent or affected another room");
});
Test("Take Two plans exclude duplicates and redraws exclude the previous four",()=>{
 var catalog=new List<CardDefinition>();var id=98000;foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend})for(var j=0;j<10;j++)catalog.Add(new(){Id=id++,Name="选牌",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true,Rarity=rarity});
 var seat=TakeTwoService.Prepare(Guid.NewGuid(),catalog,TakeTwoService.EmptyPool);Check(seat.Plans.Count==1,"incomplete profession offered");var plan=seat.Plans["Forestcraft"];Check(plan.Core.Length==2&&catalog.Single(c=>c.Id==plan.Core[0]).Rarity==CardRarity.Legend,"legendary initial core missing");
 foreach(var round in plan.Rounds){var prior=round.Offer.SelectMany(x=>x).ToArray();Check(prior.Distinct().Count()==4,"same card on both sides");foreach(var redraw in round.RedrawOffers){var next=redraw.SelectMany(x=>x).ToArray();Check(next.Distinct().Count()==4&&!next.Intersect(prior).Any(),"redraw repeated previous four");prior=next;}}
 var matches=new MatchService(NullLogger<MatchService>.Instance);var service=new TakeTwoService(matches);var host=Guid.NewGuid();var guest=Guid.NewGuid();var room=service.Create(host,"open",TakeTwoService.EmptyPool,catalog);
 var blocked=false;try{service.Act(room,room.Players[0],"class",1,"Forestcraft",0);}catch(InvalidOperationException e){blocked=e.Message.Contains("等待对手");}
 Check(blocked&&room.Players[0].Cards.Count==0&&room.Players[0].Sequence==0,"host drafted before opponent joined");
 var waiting=JsonSerializer.SerializeToElement(service.View(room,room.Players[0]));Check(!waiting.GetProperty("opponentJoined").GetBoolean()&&waiting.GetProperty("role").GetString()=="host","waiting room role wrong");
 service.Join(room.Code,guest);
 foreach(var player in room.Players){service.Act(room,player,"class",1,"Forestcraft",0);service.Act(room,player,"class",1,"Forestcraft",0);Check(player.Cards.Count==2,"retry applied twice");for(var r=0;r<14;r++)service.Act(room,player,"pick",r+2,null,r%2);Check(player.Cards.Count==30,"draft deck size incorrect");service.Act(room,player,"ready",16,null,0);}
 Check(room.Match?.Status==MatchStatus.Started,"native game bridge not started");Check(room.Match.HostDeckSelection.GeneratedDraftCardIds?.Length==30,"server-built deck lost when cloned");
 Check(!JsonSerializer.Serialize(room.Match.HostDeckSelection).Contains("GeneratedDraftCardIds"),"draft deck leaked in room response");
});
Test("Take Two local test has independent seats and requires host credentials",()=>{
 var catalog=new List<CardDefinition>();var id=98000;
 foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend}) for(var i=0;i<12;i++) catalog.Add(new(){Id=id++,Name="本机测试",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true,Rarity=rarity});
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var owner=Guid.NewGuid();var room=service.Create(owner,"open",TakeTwoService.EmptyPool,catalog);var host=room.Players[0];
 var denied=false;try{service.JoinLocalTest(room,Guid.NewGuid(),host.Token);}catch(UnauthorizedAccessException){denied=true;}Check(denied&&room.Players.Count==1,"stranger created a local seat");
 var guest=service.JoinLocalTest(room,owner,host.Token);Check(guest.PlayerId!=host.PlayerId&&guest.Token!=host.Token,"local seats share identity");Check(service.Authorize(room,owner,guest.Token)==guest&&service.Authorize(room,owner,host.Token)==host,"owner cannot independently operate local seats");
 Check(service.JoinLocalTest(room,owner,host.Token)==guest&&room.Players.Count==2,"reopening local test added another seat");
 service.Act(room,host,"class",1,"Forestcraft",0);Check(guest.Cards.Count==0&&guest.Sequence==0,"host draft altered guest");
 service.Act(room,guest,"class",1,"Forestcraft",0);
 foreach(var seat in room.Players){for(var r=0;r<14;r++)service.Act(room,seat,"pick",r+2,null,r%2);service.Act(room,seat,"ready",16,null,0);}
 Check(room.Match?.Status==MatchStatus.Started&&room.Match.HostPlayerId!=room.Match.GuestPlayerId,"local test did not start independent battle players");
 Check(room.Match!.GuestDeckSelection.UserId==owner,"local test lost account appearance preferences");
 var pvp=service.Create(Guid.NewGuid(),"open",TakeTwoService.EmptyPool,catalog);service.Join(pvp.Code,Guid.NewGuid());var blocked=false;try{service.JoinLocalTest(pvp,pvp.Players[0].PlayerId,pvp.Players[0].Token);}catch(InvalidOperationException){blocked=true;}Check(blocked&&pvp.Players.Count==2,"local test replaced a real opponent");
});
Test("Take Two pool management is admin-only and saved pools are account-specific",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var admin=new User{Id=Guid.NewGuid(),Username="卡池管理员",Email="pool-admin@example.invalid",PasswordHash="test",Role=UserRole.Admin,IsActive=true};var player=new User{Id=Guid.NewGuid(),Username="卡池玩家",Email="pool-player@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};db.Users.AddRange(admin,player);db.SaveChangesAsync().GetAwaiter().GetResult();
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var controller=new ShadowverseBeyond.Server.Controllers.TakeTwoController(db,service){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var settings=TakeTwoService.EmptyPool with{Redraws=new(){{"Forestcraft",4}}};controller.Request.Headers["X-Editor-UserId"]=player.Id.ToString();Check(controller.SaveSettings("rotation",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.StatusCodeResult denied&&denied.StatusCode==403,"player edited system pool");
 var poolId=Guid.NewGuid();Check(controller.SavePool(poolId,new("我的卡池",settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"personal pool could not be saved");Check(db.FeatureDocuments.Any(d=>d.Id==$"take-two-saved:{player.Id}:{poolId}"),"personal account namespace missing");
 controller.Request.Headers["X-Editor-UserId"]=admin.Id.ToString();Check(controller.SaveSettings("rotation",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"admin pool save failed");Check(controller.SaveSettings("rotation",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"pool revision overwritten");
 controller.DeletePool(poolId,default).GetAwaiter().GetResult();Check(db.FeatureDocuments.Any(d=>d.Id==$"take-two-saved:{player.Id}:{poolId}"),"another account deleted the saved pool");
});
Test("AS2P uses unrestricted rounds and guarantees a legend on rounds 7 and 14 including redraws",()=>{
 var catalog=new List<CardDefinition>();var id=97000;
 foreach(var cls in new[]{CardClass.Forestcraft,CardClass.Swordcraft}) foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Gold,CardRarity.Legend}) for(var i=0;i<12;i++) catalog.Add(new(){Id=id++,Name="AS2P测试",Class=cls,Type=CardType.Follower,IsCollectible=true,Rarity=rarity,CardSet="AS2P包"});
 var neutral=new CardDefinition{Id=id++,Name="精灵专属中立",Class=CardClass.Neutral,Type=CardType.Follower,IsCollectible=true,Rarity=CardRarity.Legend,CardSet="其他包"};catalog.Add(neutral);
 var common=new CardDefinition{Id=id++,Name="共同中立",Class=CardClass.Neutral,Type=CardType.Spell,IsCollectible=true,Rarity=CardRarity.Gold,CardSet="AS2P包"};catalog.Add(common);
 var settings=TakeTwoService.EmptyPool with {Packs=["AS2P包"],NeutralByClass=new(){{"Forestcraft",[neutral.Id]}}};
 var pool=TakeTwoService.PoolCards(catalog,"as2p",settings);
 Check(pool.Any(c=>c.Id==neutral.Id),"neutral supplement outside packs absent");
 Check(TakeTwoService.ClassPool(pool,"as2p",settings,CardClass.Forestcraft).Any(c=>c.Id==neutral.Id),"assigned neutral absent");
 Check(!TakeTwoService.ClassPool(pool,"as2p",settings,CardClass.Swordcraft).Any(c=>c.Id==neutral.Id),"neutral leaked to unassigned class");
 Check(TakeTwoService.ClassPool(pool,"as2p",settings,CardClass.Swordcraft).Any(c=>c.Id==common.Id),"shared base neutral absent");
 var seat=TakeTwoService.Prepare(Guid.NewGuid(),pool,settings,"as2p");Check(seat.Plans.Count==2,"AS2P incorrectly required silver cards");
 foreach(var plan in seat.Plans.Values) for(var index=0;index<14;index++) {
  var round=plan.Rounds[index];int[]? previous=null;
  foreach(var offer in new[]{round.Offer}.Concat(round.RedrawOffers)) {
   var four=offer.SelectMany(x=>x).ToArray();Check(four.Distinct().Count()==4,"duplicate offer");Check(previous==null||!four.Intersect(previous).Any(),"redraw repeated prior four");
   if(index is 6 or 13) {var cards=four.Select(id=>catalog.Single(c=>c.Id==id)).ToArray();Check(cards.All(c=>c.Rarity is CardRarity.Gold or CardRarity.Legend)&&cards.Any(c=>c.Rarity==CardRarity.Legend),"special offer rarity guarantee lost");}
   previous=four;
  }
 }
 var small=catalog.Where(c=>c.Class==CardClass.Forestcraft&&c.Rarity!=CardRarity.Legend).Concat(catalog.Where(c=>c.Class==CardClass.Forestcraft&&c.Rarity==CardRarity.Legend).Take(2)).ToList();
 for(var attempt=0;attempt<20;attempt++) Check(TakeTwoService.Prepare(Guid.NewGuid(),small,settings,"as2p").Plans.Count==1,"two-legend pool randomly became unavailable during redraw generation");
 Check(TakeTwoService.PoolCards(catalog,"as2p",TakeTwoService.EmptyPool).Count==0,"unconfigured AS2P silently used full catalog");
 var roomService=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var room=roomService.Create(Guid.NewGuid(),"as2p",settings,catalog);roomService.Join(room.Code,Guid.NewGuid());Check(room.Players.All(p=>p.Plans.Count==2)&&room.Environment=="as2p","room/join lost AS2P rules");
});
Test("AS2P settings are admin-only and validate neutral assignments",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var admin=new User{Id=Guid.NewGuid(),Username="AS2P管理员",Email="as2p-admin@example.invalid",PasswordHash="test",Role=UserRole.Admin,IsActive=true};var player=new User{Id=Guid.NewGuid(),Username="AS2P玩家",Email="as2p-player@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};db.Users.AddRange(admin,player);db.CardDefinitions.AddRange(new CardDefinition{Id=96901,Name="中立",Class=CardClass.Neutral,Type=CardType.Follower,IsCollectible=true},new CardDefinition{Id=96902,Name="精灵",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true});db.SaveChangesAsync().GetAwaiter().GetResult();
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var controller=new ShadowverseBeyond.Server.Controllers.TakeTwoController(db,service){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var settings=TakeTwoService.EmptyPool with {NeutralByClass=new(){{"Forestcraft",[96901]}}};controller.Request.Headers["X-Editor-UserId"]=player.Id.ToString();Check(controller.SaveSettings("as2p",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.StatusCodeResult denied&&denied.StatusCode==403,"player edited AS2P settings");
 controller.Request.Headers["X-Editor-UserId"]=admin.Id.ToString();Check(controller.SaveSettings("as2p",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"admin could not configure AS2P");
 var saved=JsonSerializer.Deserialize<TakeTwoPool>(db.FeatureDocuments.Single(d=>d.Id=="take-two:as2p").Json,CardStyleCatalog.JsonOptions)!;Check(saved.NeutralByClass["Forestcraft"].Single()==96901,"neutral assignments not persisted");
 Check(controller.SaveSettings("as2p",new(1,settings with{NeutralByClass=new(){{"Forestcraft",[96902]}}}),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"non-neutral assignment accepted");
 Check(controller.SaveSettings("as2p",new(1,settings with{NeutralByClass=new(){{"99",[96901]}}}),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.BadRequestResult,"unknown class accepted");
});
AbilityConfig Choice() => new() { Targets = new() { ["one"] = new() { PlayerChoiceOptions = new() { IsRequired = true } } } };
Test("skip direct target selection", () => Check(AbilitySelectionPolicy.RequiresPlayerSelection(Choice()),"target"));
Test("skip mode after earlier effects atomically", () => Check(AbilitySelectionPolicy.RequiresPlayerSelection(new() { Effects = new() { new() {Type=EffectType.Heal}, new() {Type=EffectType.Choose} } }),"mode"));
Test("allow automatic targets", () => Check(!AbilitySelectionPolicy.RequiresPlayerSelection(new() {Targets=new(){["one"]=new(){PlayerChoiceOptions=new(){IsRequired=false}}}}),"auto"));
Test("ignore disabled target", () => { var c=Choice();c.Targets["one"].IsEnabled=false;Check(!AbilitySelectionPolicy.RequiresPlayerSelection(c),"disabled"); });
Test("skip choice in chained ability", () => Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=EffectType.Draw,ChainedAbilities=new(){Choice()}}}}),"chain"));
foreach(var type in new[]{EffectType.Sequence,EffectType.Random}) {
 Test($"skip choice inside {type}",()=> Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=type,Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=new[]{Choice()}}}}}}),"branch"));
 Test($"allow pure branches inside {type}",()=> Check(!AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=type,Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=new[]{new AbilityConfig()}}}}}}),"pure"));
}
Test("serialized branch inspection",()=>Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=EffectType.Sequence,Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=JsonSerializer.Deserialize<JsonElement>("[{\"targets\":{\"a\":{\"playerChoiceOptions\":{\"isRequired\":true}}}}]")}}}}}),"json"));
Test("cycle cannot partially invoke",()=>{var c=new AbilityConfig();c.Effects.Add(new(){ChainedAbilities=new(){c}});Check(AbilitySelectionPolicy.RequiresPlayerSelection(c),"cycle");});
Test("legacy defaults keep trigger behavior",()=>Check(!new TriggerAbilityEffectParams().SkipTargetAndModeSelection,"default"));
foreach(var keyword in new[]{AbilityKeyword.Accelerate,AbilityKeyword.Crystalize}) Test($"normalize {keyword} effect declaration",()=>{var c=new AbilityConfig{AbilityKeywords=new(){new(){Keyword=keyword,Value=0}},Effects=new(){new(){Type=keyword==AbilityKeyword.Accelerate?EffectType.Accelerate:EffectType.Crystalize,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=7}}}}};c.NormalizeKeywords();Check(c.AlternatePlay?.PlayAsCardDefinitionId==7,"declaration lost");});

var definitions=new Dictionary<int,CardDefinition>();
for(int i=1;i<=9;i++)definitions[i]=new(){Id=i,Name=$"Card {i}",Cost=i,Type=CardType.Follower,Attack=i,Health=i+1};
definitions[7].ArtPreview="/original.png";definitions[8].ArtPreview="/spell.png";definitions[9].ArtPreview="/amulet.png";
definitions[8].Type=CardType.Spell;definitions[9].Type=CardType.Amulet;
var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAbilityConfigs"=>new List<AbilityConfig>(),"GetAllCardDefinitions"=>definitions,"get_Cards"=>definitions.Values.ToList(),_=>null});
GameDataProvider.SetInstanceForTesting(data);
var activeAbilities = new List<IAbility>();
var abilityManager=Proxy<IAbilityManager>.Make((method,args)=>method.Name switch {"GetAbilitiesForCard" or "GetEffectiveAbilitiesForCard" or "CreateAbilitiesForCard"=>activeAbilities,_=>null});
var buffs=new BuffManager(NullLogger<BuffManager>.Instance,abilityManager);
var buffFactory=new BuffFactory(NullLogger<BuffFactory>.Instance,data);
var evaluator=new BuffEvaluator(Proxy<IConditionEvaluator>.Make((m,a)=>true),NullLogger<BuffEvaluator>.Instance);
var buffService=new BuffService(buffs,evaluator,buffFactory,NullLogger<BuffService>.Instance,new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
var factory=new CardFactory(NullLogger<CardFactory>.Instance,buffFactory,buffs);
var compiler=new DslCompiler(NullLogger<DslCompiler>.Instance,new FunctionRegistry(NullLogger<FunctionRegistry>.Instance),new DslExpressionCache(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),data);
var accessor=new GameContextAccessor(); GameServiceProvider.ContextAccessor=accessor;
var services=new ServiceCollection().AddSingleton(data).AddSingleton<IBuffService>(buffService).AddSingleton<ICardFactory>(factory).AddSingleton<IDslCompiler>(compiler).AddSingleton<IConditionEvaluator>(new DslConditionEvaluator(compiler,NullLogger<DslConditionEvaluator>.Instance)).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
var p1=new PlayerState(Guid.NewGuid());var p2=new PlayerState(Guid.NewGuid());
var game=new GameState("migration-tests",p1,p2,p1.PlayerId){AbilityManager=abilityManager,BuffManager=buffs};
p1.Leader=factory.CreateAndRegisterNewCard(game,1,p1.PlayerId,CardZone.Staging);p2.Leader=factory.CreateAndRegisterNewCard(game,1,p2.PlayerId,CardZone.Staging);
using var context=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
Test("inverse of all candidates resolves to an explicit empty target list", () => {
 var query = Proxy<IQueryExecutor>.Make((m,a) => new QueryResult { Results = new object[] { p1.Leader } });
 var pending = Proxy<IPendingActionService>.Make((m,a) => throw new Exception("automatic targets should not prompt"));
 var selection = new TargetSelectionService(NullLogger<TargetSelectionService>.Instance, query, pending);
 using var provider = new ServiceCollection().AddSingleton(query).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var ctx = new GameContext(provider, new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
 foreach (var (required, inverse, expected) in new[] { (false, false, 1), (false, true, 0), (true, true, 0) }) {
  var config = new TargetConfig { PlayerChoiceOptions = new() { IsRequired = required, MinCount = 2, MaxCount = 2, IsInverseSelection = inverse } };
  var target = new Target("cards", config, selection, NullLogger<Target>.Instance);
  var ability = Proxy<IAbility>.Make((m,a) => m.Name switch { "get_Targets" => new List<Target> { target }, "HasKeyword" => false, _ => null });
  var resolved = selection.ResolveTargetsForAbilityAsync(ability,ctx,false).GetAwaiter().GetResult();
  Check(resolved.Status == ResolutionStatus.Completed && resolved.ResolvedTargets["cards"].Count == expected, "wrong complement or missing pool");
 }
});
CardInstance Destroyed(int id) {var card=factory.CreateAndRegisterNewCard(game,id,p1.PlayerId,CardZone.Graveyard); typeof(CardInstance).GetMethod("SetLastMoveReason",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(card,new object[]{CardMoveReason.Destroyed});return card;}
var reanimate=new ReanimateEffectHandler(NullLogger<ReanimateEffectHandler>.Instance,new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),factory);
GameStateActionResult Reanimate(int maximum,List<ConditionConfig>? conditions=null) => reanimate.Apply(new Effect(p1.PlayerId),context,new(){MaxCost=maximum,PoolConditions=conditions??new()});
int SummonedId(GameStateActionResult result)=>result.GeneratedEvents.OfType<SummonEvent>().Single().Target.Single().Card!.BaseDefinitionId;
Test("reanimate highest destroyed base cost",()=>{p1.Graveyard.Clear();Destroyed(3);Destroyed(5);Check(SummonedId(Reanimate(4))==3,"highest");});
Test("filter pool before highest cost",()=>{p1.Graveyard.Clear();Destroyed(3);Destroyed(5);Check(SummonedId(Reanimate(9,new(){new(){Expression="self.BaseCost == 3"}}))==3,"filter ignored");});
Test("disabled pool filter ignored",()=>Check(SummonedId(Reanimate(9,new(){new(){Expression="false",IsEnabled=false}}))==5,"disabled"));
Test("filter applies to candidate not invoking card",()=>Check(SummonedId(Reanimate(9,new(){new(){Expression="self.BaseDefinitionId == 5"}}))==5,"self"));
Test("stacked reanimate keyword corrections",()=>{p1.Graveyard.Clear();Destroyed(3);Destroyed(5);for(int i=0;i<2;i++)buffs.RegisterBuffInstance(new Buff(Guid.Empty,p1.Leader.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Reanimate,1,ModifierLayer.Additive)})}),game);Check(SummonedId(Reanimate(3))==5,"stack");});
Test("non-destroyed graveyard entries excluded",()=>{p1.Graveyard.Clear();factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Graveyard);Destroyed(2);Check(SummonedId(Reanimate(9))==2,"reason");});
Test("other owner's pool excluded",()=>{p1.Graveyard.Clear();Destroyed(2);var other=factory.CreateAndRegisterNewCard(game,7,p2.PlayerId,CardZone.Graveyard);typeof(CardInstance).GetMethod("SetLastMoveReason",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(other,new object[]{CardMoveReason.Destroyed});Check(SummonedId(Reanimate(9))==2,"owner");});
foreach(var zone in new[]{CardZone.Field,CardZone.Hand,CardZone.Deck,CardZone.Graveyard,CardZone.Extra,CardZone.Emblem}) Test($"transform new entity identity in {zone}",()=>{var list=zone switch{CardZone.Field=>p1.Field,CardZone.Hand=>p1.Hand,CardZone.Deck=>p1.Deck,CardZone.Graveyard=>p1.Graveyard,CardZone.Extra=>p1.Extra,_=>p1.Emblem};list.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,zone);var oldId=old.InstanceId;var index=list.IndexOf(old);var ev=new TransformEvent(old,3);Check(ev.Commit(context).Success,"commit");var current=list[index];Check(current.InstanceId!=oldId&&current.BaseDefinitionId==3,"identity");Check(old.CurrentZone==CardZone.Removed&&game.FindCardInstance(oldId)==null,"old remains");Check(game.FindCardInstance(current.InstanceId)==current,"registry");Check(ev.TransformingCard==current&&ev.TransformingCardSnapshot!.InstanceId==oldId,"event identity");});
Test("stale targets do not point at transformed card",()=>{p1.Field.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);var target=TargetInfo.ForCard(old);var ev=new TransformEvent(old,4);ev.Commit(context);Check(target.Card!=ev.TransformingCard&&target.InstanceId!=ev.TransformingCard!.InstanceId,"stale target rebound");});
Test("reveal identifies owner without mutating hand",()=>{p2.Hand.Clear();var card=factory.CreateAndRegisterNewCard(game,4,p2.PlayerId,CardZone.Hand);var effect=new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}};var r=new RevealEffectHandler(NullLogger<RevealEffectHandler>.Instance).Apply(effect,context,new());var ev=r.GeneratedEvents.OfType<CardRevealedEvent>().Single();Check(ev.Source!.Player==p2,"wrong owner");ev.Commit(context);Check(p2.Hand.Count==1&&card.CurrentZone==CardZone.Hand,"reveal moved card");});

Test("empty filtered pool is harmless",()=>Check(Reanimate(9,new(){new(){Expression="false"}}).GeneratedEvents.Count==0,"empty"));
Test("all enabled filters intersect",()=>{p1.Graveyard.Clear();Destroyed(2);Destroyed(3);Destroyed(5);Check(SummonedId(Reanimate(9,new(){new(){Expression="self.BaseCost > 2"},new(){Expression="self.BaseCost < 5"}}))==3,"intersection");});
foreach (var positions in new[] { new[]{1}, new[]{2,0} }) Test("mulligan retains selected slots " + string.Join(",",positions),()=>{
 var a=new PlayerState(Guid.NewGuid());var b=new PlayerState(Guid.NewGuid());var match=new GameState("mulligan-slots",a,b,a.PlayerId){AbilityManager=abilityManager,BuffManager=buffs};
 var original=Enumerable.Range(0,4).Select(_=>factory.CreateAndRegisterNewCard(match,2,a.PlayerId,CardZone.Hand)).ToList();
 for(int i=0;i<5;i++)factory.CreateAndRegisterNewCard(match,3,a.PlayerId,CardZone.Deck);
 var result=a.PerformMulligan(positions.Select(i=>original[i].InstanceId).ToList(),match);
 Check(result.Success&&a.Hand.Count==4&&a.Deck.Count==5,"zone sizes changed");
 for(int i=0;i<4;i++)if(!positions.Contains(i))Check(a.Hand[i]==original[i],"kept slot moved");
 Check(a.Hand.Concat(a.Deck).Select(c=>c.InstanceId).Distinct().Count()==9,"card identity lost");
});
Test("mulligan can draw a returned card again",()=>{
 var a=new PlayerState(Guid.NewGuid());var b=new PlayerState(Guid.NewGuid());var match=new GameState("mulligan-returned",a,b,a.PlayerId){AbilityManager=abilityManager,BuffManager=buffs};
 var card=factory.CreateAndRegisterNewCard(match,2,a.PlayerId,CardZone.Hand);
 Check(a.PerformMulligan(new(){card.InstanceId},match).Success&&a.Hand.Single()==card,"returned card was excluded");
});
Test("reanimate modifier applies only to selected player",()=>{p1.Graveyard.Clear();Destroyed(5);Check(buffService.GetEffectiveAbilityKeywordValue(p2.Leader,AbilityKeyword.Reanimate,3,context)==3,"opponent polluted");});
foreach(var mode in new[]{AbilityKeyword.Accelerate,AbilityKeyword.Crystalize}) {
 Test($"zero-cost {mode} is playable",()=>{
   var alternate=new AlternatePlayConfig{PlayAsCardDefinitionId=mode==AbilityKeyword.Accelerate?8:9};
   activeAbilities.Clear(); activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{
     "get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=mode,Value=0}},
     "get_AlternatePlay"=>alternate,"get_RuntimeId"=>Guid.NewGuid(),"HasKeyword"=>(AbilityKeyword)args![0]! == mode,_=>null }));
   var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);p1.CurrentPlayPoints=0;
   var resolution=PlayModeResolver.Resolve(card,p1,context);Check(resolution.SelectedCost==0 && resolution.SelectedAlternateCardDefinitionId==alternate.PlayAsCardDefinitionId,"zero cost");
   activeAbilities.Clear();
 });
}
Test("alternate type mismatch is unavailable",()=>{
 activeAbilities.Clear();activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{"get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=AbilityKeyword.Accelerate,Value=0}},"get_AlternatePlay"=>new AlternatePlayConfig{PlayAsCardDefinitionId=9},"HasKeyword"=>true,_=>null}));
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);p1.CurrentPlayPoints=0;Check(!PlayModeResolver.Resolve(card,p1,context).HasAvailableMode,"wrong type accepted");activeAbilities.Clear();
});
Test("transform removes old counters, damage and modifiers",()=>{
 p1.Field.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);old.SetCounter("example",9);old.Damage=1;old.IsEvolved=true;
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,old.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-2,ModifierLayer.Additive)})}),game);
 var ev=new TransformEvent(old,3);Check(ev.Commit(context).Success,"transform");var current=ev.TransformingCard!;
 Check(current.GetCounter("example")==0&&current.Damage==0&&!current.IsEvolved,"runtime carried");Check(buffs.GetBuffsForCard(current.InstanceId).Count==0,"buff carried");
});

foreach(var mode in new[]{PlayMode.Accelerate,PlayMode.Crystalize}) Test($"{mode} pays once and preserves preflight entity",()=>{
 activeAbilities.Clear();p1.Hand.Clear();p1.Field.Clear();p1.MaxPlayPoints=2;p1.CurrentPlayPoints=2;var old=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);var combo=p1.Combo;
 var ev=new PlayCardEvent(p1,old,mode,paidCost:1,alternateCardDefinitionId:mode==PlayMode.Accelerate?8:9);var result=ev.Commit(context);
 Check(result.Success,"play failed: "+result.ErrorMessage);Check(result.GeneratedEvents.OfType<PpUpdateEvent>().Count()==1,"payment event count");foreach(var payment in result.GeneratedEvents.OfType<PpUpdateEvent>())payment.Commit(context);Check(p1.CurrentPlayPoints==1,"PP charged twice");Check(p1.Combo==combo+1,"combo charged twice");Check(ev.PlayedCard!.InstanceId==old.InstanceId && ReferenceEquals(ev.PlayedCard,old),"preflight identity lost");Check(!p1.Hand.Any(card=>card.BaseDefinitionId==7),"original definition remains");Check(ev.PlayedCard.BaseDefinitionId==(mode==PlayMode.Accelerate?8:9),"wrong card definition");Check(ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(ev.PlayedCard,context).ArtPreview=="/original.png","original art lost");
});
Test("transform event transports old-to-new identity",()=>{
 activeAbilities.Clear();p1.Field.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);var ev=new TransformEvent(old,3);ev.Commit(context);
 var processor=new EventProcessor(NullLogger<EventProcessor>.Instance,services.GetRequiredService<IEventHistoryService>());
 var method=typeof(EventProcessor).GetMethod("ConvertTransformEvent",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var dto=(ShadowverseBeyond.Server.Dtos.SignalR.Events.CardInstanceUpdatedEventDto)method.Invoke(processor,new object[]{ev,context})!;
 Check(dto.ReplacedCardInstanceIds[old.InstanceId]==ev.TransformingCard!.InstanceId,"transport mapping missing");
});
Test("stacked numeric attached effects remain visible",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 for(int i=0;i<2;i++)buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){EffectKey="stacked",SourceCardName="Source",SourceAbilityName="Discount"},game);
 var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.BuffSummaries.Single().Details.Count==2,"stacked amounts lost");
});

Test("applied permanent numeric buffs combine across source turns",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 for(int i=0;i<2;i++) buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,i,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-2,ModifierLayer.Additive)})}){EffectKey="turn"+i,SourceCardName="Source"+i,SourceAbilityName="Discount"+i},game);
 var summaries=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries;
 Check(summaries.Count==2,"sources lost");Check(summaries.SelectMany(x=>x.NumericEffects).Select(x=>x.PolicyKey).Distinct().Count()==1,"source turn incorrectly separates permanent buffs");
});
Test("reveal transport includes hand position and log announcement",()=>{
 p2.Hand.Clear();var card=factory.CreateAndRegisterNewCard(game,4,p2.PlayerId,CardZone.Hand);var ev=new CardRevealedEvent(p2,card);
 var processor=new EventProcessor(NullLogger<EventProcessor>.Instance,services.GetRequiredService<IEventHistoryService>());
 var method=typeof(EventProcessor).GetMethod("ConvertCardRevealedEvent",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var dto=(ShadowverseBeyond.Server.Dtos.SignalR.Events.CardRevealedEventDto)method.Invoke(processor,new object[]{ev,context})!;
 Check(dto.HandIndex==0&&dto.RevealingPlayerId==p2.PlayerId,"hand origin lost");Check(ev.GetAnnouncement(game).Contains("公开了"),"log missing");
});
Test("granted ability names carry rules text when description is empty",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 var text="于手牌中发动。当自己使用其他卡片时，舍弃这张卡片";
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityBuffEffect(new(){new AbilityConfig{Name=text,Description=""}})}){SourceCardName="荒诞夜恸",SourceAbilityName="Ability1"},game);
 var summary=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries.Single();Check(summary.GrantedAbilities.Single().Name==text,"rules text missing");Check(summary.Details.Contains(text),"fallback description missing");
});
Test("stacked one-use Act modifiers survive reads and expire after successful use",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 for(int i=0;i<2;i++)buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,i,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){RemoveAfterUse=true,SourceCardName="双界异兽",SourceAbilityName="减费"},game);
 Check(buffService.GetEffectiveAbilityKeywordValue(card,AbilityKeyword.Act,2,context)==0,"stacked cost incorrect");
 Check(buffService.GetEffectiveAbilityKeywordValue(card,AbilityKeyword.Act,2,context)==0,"reading consumed reduction");
 var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.BuffSummaries.SelectMany(x=>x.NumericEffects).All(x=>x.RemoveAfterUse),"one-use display flag lost");
 var result=new ActInvokedEvent(p1,card,0).Commit(context);Check(result.Success,"Act failed");Check(buffService.GetEffectiveAbilityKeywordValue(card,AbilityKeyword.Act,2,context)==2,"reduction not consumed");
 Check(!buffs.GetBuffsForCard(card.InstanceId).Any(x=>x.IsActive),"consumed effect still active");Check(result.GeneratedEvents.OfType<CardInstanceUpdatedEvent>().Any(),"UI not refreshed");
 var copy=buffFactory.CreateBuffCopy(buffs.GetBuffsForCard(card.InstanceId).First());Check(copy.RemoveAfterUse&&!copy.IsActive,"copy revived consumed effect");
});
Test("failed Act does not consume one-use cost modifiers",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 var buff=new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){RemoveAfterUse=true};buffs.RegisterBuffInstance(buff,game);
 var result=new ActInvokedEvent(p1,card,999).Commit(context);Check(!result.Success&&buff.IsActive,"failed action consumed buff");
});
Test("destroy snapshots exist before trigger condition evaluation",()=>{
 var target=factory.CreateAndRegisterNewCard(game,2,p2.PlayerId,CardZone.Field);var source=factory.CreateAndRegisterNewCard(game,3,p1.PlayerId,CardZone.Field);
 var ev=new DestroyEvent(target,source,EventReasonSource.Ability);Check(ev.DestroyTargetSnapshot!=null&&ev.DestroySourceSnapshot!=null,"snapshot missing before On");
 Check(ev.DestroyTargetSnapshot.InstanceId==target.InstanceId,"wrong snapshot");
});
Test("inactive attached effects are hidden",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){IsActive=false,SourceCardName="Source",SourceAbilityName="Discount"},game);
 Check(ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries.Count==0,"inactive still shown");
});
Test("unsourced active effects retain readable numeric details",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-2,ModifierLayer.Additive)})}),game);
 Check(ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries.Single().Details.Single().Contains("-2"),"details hidden");
});
Test("runtime alternate preview includes referenced face data",()=>{
 activeAbilities.Clear();activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{"get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=AbilityKeyword.Accelerate,Value=0}},"get_AlternatePlay"=>new AlternatePlayConfig{PlayAsCardDefinitionId=8},"HasKeyword"=>(AbilityKeyword)args![0]! == AbilityKeyword.Accelerate,_=>null}));
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.AlternatePlayReferences.Single().Id==8,"reference missing");Check(dto.AlternatePlayReferences.Single().Name==definitions[8].Name,"face missing");activeAbilities.Clear();
});
Test("alternate availability conditions are evaluated",()=>{
 activeAbilities.Clear();activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{"get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=AbilityKeyword.Accelerate,Value=0}},"get_AlternatePlay"=>new AlternatePlayConfig{PlayAsCardDefinitionId=8,AvailabilityConditions=new(){new(){Expression="false"}}},"HasKeyword"=>(AbilityKeyword)args![0]! == AbilityKeyword.Accelerate,_=>null}));
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);p1.CurrentPlayPoints=0;Check(!PlayModeResolver.Resolve(card,p1,context).HasAvailableMode,"condition ignored");activeAbilities.Clear();
});
var options=new JsonSerializerOptions{WriteIndented=true};Test("crystallize preflight does not run Last Words or Act while the card is played",()=>{
 var configs=new List<AbilityConfig>{
  new(){Name="月兔妖谢幕曲",Trigger=new(){EventTypes=new(){EventType.Destroy},RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On}},
  new(){Name="月兔妖启动",Trigger=new(){EventTypes=new(){EventType.Act},RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On}},
  new(){Name="合法入场曲",Trigger=new(){EventTypes=new(){EventType.PlayCard},RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On}}
 };
 var selection=(TargetSelectionService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TargetSelectionService));
 var conditionFactory=Proxy<IConditionFactory>.Make((m,a)=>null);var targetFactory=Proxy<ITargetFactory>.Make((m,a)=>null);
 var alternateData=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>definitions[9],"GetAbilityConfigs"=>configs,_=>null});
 var alternateFactory=Proxy<IAbilityFactory>.Make((m,a)=>m.Name=="CreateAbilities"?configs.Select(config=>new Ability(config,conditionFactory,targetFactory,selection,(CardInstance)a![0]!)).ToList():null);
 var provider=new ServiceCollection().AddSingleton(alternateData).AddSingleton(alternateFactory).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var testContext=new GameContext(provider,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
 var transaction=(GameActionTransactionService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GameActionTransactionService));
 var method=typeof(GameActionTransactionService).GetMethod("ResolveAlternatePlayAbilitiesAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var pending=(Task)method.Invoke(transaction,new object[]{testContext,new PlayCardEvent(p1,p1.Leader,PlayMode.Crystalize,1,null,9),TriggerTiming.On})!;pending.GetAwaiter().GetResult();
 var result=pending.GetType().GetProperty("Result")!.GetValue(pending)!;
 var abilities=(List<IAbility>)result.GetType().GetField("Item2")!.GetValue(result)!;
 Check(abilities.Count==1&&abilities[0].Name=="合法入场曲","unrelated alternate card abilities ran during play");
});
definitions[10]=new(){Id=10,Name="证据护符",Type=CardType.Amulet,Keywords=new(){nameof(CardKeyword.Evidence)}};
definitions[11]=new(){Id=11,Name="土之印护符",Type=CardType.Amulet,Keywords=new(){nameof(CardKeyword.EarthSigil)}};
var evidenceSettlement=new SettleEvidenceEffectHandler(NullLogger<SettleEvidenceEffectHandler>.Instance);
var evidenceModifier=new ModifyEvidenceEffectHandler(NullLogger<ModifyEvidenceEffectHandler>.Instance,new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
void CommitEvidenceEvents(IEnumerable<IGameEvent> events){var queue=new Queue<IGameEvent>(events);var steps=0;while(queue.Count>0){Check(++steps<100,"evidence event loop");if(queue.Dequeue() is ICommittableEvent ev){var committed=ev.Commit(context);Check(committed.Success,"evidence event commit failed: "+committed.ErrorMessage);foreach(var next in committed.GeneratedEvents)queue.Enqueue(next);}}}
Test("evidence enters with one and merges only friendly evidence amulets by vanishing",()=>{
 activeAbilities.Clear();p1.Field.Clear();p2.Field.Clear();
 var old=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);old.SetCounter(CardInstance.EvidenceCounterKey,3);
 var old2=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);old2.SetCounter(CardInstance.EvidenceCounterKey,2);
 var earth=factory.CreateAndRegisterNewCard(game,11,p1.PlayerId,CardZone.Field);
 var enemy=factory.CreateAndRegisterNewCard(game,10,p2.PlayerId,CardZone.Field);enemy.SetCounter(CardInstance.EvidenceCounterKey,8);
 var entering=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);Check(entering.Evidence==1,"initial evidence not one");
 var result=evidenceSettlement.Apply(new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(entering)}},context,new());
 Check(result.Success&&entering.Evidence==6,"merged evidence total wrong");Check(result.GeneratedEvents.OfType<LeaveFieldEvent>().Count()==2&&result.GeneratedEvents.OfType<LeaveFieldEvent>().All(e=>e.RequestedKind==LeaveFieldKind.Vanish),"old evidence not vanished");
 CommitEvidenceEvents(result.GeneratedEvents);Check(old.CurrentZone==CardZone.Removed&&old2.CurrentZone==CardZone.Removed,"merged amulets still on field");Check(earth.CurrentZone==CardZone.Field&&earth.Earthsigil==1&&enemy.Evidence==8&&enemy.CurrentZone==CardZone.Field,"earth or opponent evidence affected");
});
Test("evidence cannot be chosen by enemy abilities but is not immune to untargeted effects or ability destruction",()=>{
 p1.Field.Clear();p2.Field.Clear();var own=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var enemy=factory.CreateAndRegisterNewCard(game,10,p2.PlayerId,CardZone.Field);
 var targets=new List<TargetInfo>{TargetInfo.ForCard(own),TargetInfo.ForCard(enemy)};
 var method=typeof(TargetSelectionService).GetMethod("ApplyAbilityTargetKeywordRules",BindingFlags.Static|BindingFlags.NonPublic)!;
 var chosen=(List<TargetInfo>)method.Invoke(null,new object[]{targets,new TargetConfig(),context,true,true})!;
 Check(chosen.Count==1&&chosen[0].Card==own,"enemy could choose evidence or own evidence blocked");
 var automatic=(List<TargetInfo>)method.Invoke(null,new object[]{targets,new TargetConfig(),context,false,true})!;Check(automatic.Count==2,"random/all ability effects blocked");
 Check(enemy.CanBeDestroyed(context,EventReasonSource.Ability),"evidence incorrectly gained earth destruction protection");
 var earth=factory.CreateAndRegisterNewCard(game,11,p2.PlayerId,CardZone.Field);Check(!earth.CanBeDestroyed(context,EventReasonSource.Ability),"earth destruction protection regressed");
});
Test("evidence add set consume and insufficient consumption preserve counter semantics",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var effect=new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}};
 var added=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Add,Amount=3});Check(added.Success&&card.Evidence==4,"add failed");
 var set=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Set,Amount=7});Check(set.Success&&card.Evidence==7,"set failed");
 var consumed=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Consume,Amount=2});Check(consumed.Success&&card.Evidence==5,"consume failed");
 var insufficient=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Consume,Amount=6});Check(!insufficient.Success&&card.Evidence==5&&!insufficient.GeneratedEvents.Any(),"insufficient consume mutated evidence");
 var invalid=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Add,Amount=-1});Check(!invalid.Success&&card.Evidence==5,"negative evidence accepted");
 var fromPlayer=evidenceModifier.Apply(new Effect(p1.PlayerId),context,new(){Amount=1,PlayerScope=PlayerScope.Self});Check(fromPlayer.Success&&card.Evidence==6,"player scope did not resolve evidence");
 var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.Evidence==6&&dto.Counters[CardInstance.EvidenceCounterKey]==6,"network counter snapshot missing");
 Check(CardInstanceSnapshot.From(card).Evidence==6&&card.Counters.Evidence==6,"DSL snapshot evidence missing");
});
foreach(var operation in new[]{CounterOperation.Set,CounterOperation.Consume}) Test($"evidence reaching zero by {operation} is system-destroyed",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var result=evidenceModifier.Apply(new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}},context,new(){Operation=operation,Amount=operation==CounterOperation.Set?0:1});
 Check(result.Success&&card.Evidence==0,"zero modification failed");var counter=result.GeneratedEvents.OfType<CounterChangedEvent>().Single();var zero=counter.Commit(context);Check(zero.GeneratedEvents.OfType<LeaveFieldEvent>().Single().ReasonSource==EventReasonSource.System,"zero did not create system destruction");CommitEvidenceEvents(zero.GeneratedEvents);Check(card.CurrentZone==CardZone.Graveyard,"zero evidence did not reach graveyard");
});
Test("generic variable effects also destroy depleted evidence without affecting other counters",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var generic=new ModifyVariableEffectHandler(NullLogger<ModifyVariableEffectHandler>.Instance,new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
 var result=generic.Apply(new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}},context,new(){TargetType=TargetType.Card,CounterName="EVIDENCE",Operation=CounterOperation.Set,Amount=0});
 CommitEvidenceEvents(result.GeneratedEvents);Check(card.CurrentZone==CardZone.Graveyard,"generic evidence modification bypassed zero destruction");
 var earth=factory.CreateAndRegisterNewCard(game,11,p1.PlayerId,CardZone.Field);earth.SetCounter("example",0);Check(!new CounterChangedEvent(earth,"example",1,0).Commit(context).GeneratedEvents.Any(),"unrelated zero counter destroyed card");
});
Test("evidence system ability responds to both summon causes and is registered with editable Chinese metadata",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var system=new ShadowverseBeyond.Server.Domain.Abilities.System.EvidenceSystemAbility();
 foreach(var reason in new[]{EventReasonSource.System,EventReasonSource.Ability}){var summon=new SummonEvent(p1,card,reasonSource:reason);Check(system.CanTrigger(summon,context).GetAwaiter().GetResult(),"evidence summon trigger missing");using var summonContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:summon);var resolved=system.ResolveTargetsAsync(summonContext).GetAwaiter().GetResult();Check(system.CreateEffectsAsync(summonContext,resolved).GetAwaiter().GetResult().Single().Type==EffectType.SettleEvidence,"system did not enqueue settlement");}
 var metadata=new EffectUIMetadataService(new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance));var entry=metadata.GetEffectUIMetadata(EffectType.ModifyEvidence)!;Check(entry.DisplayName=="证据数修改"&&entry.CategoryLabel=="资源数值"&&entry.Parameters.Any(p=>p.EnumName==nameof(CounterOperation)),"evidence editor metadata missing");Check(!metadata.GetAllEffectUIMetadata().Any(m=>m.EffectTypeName==nameof(EffectType.SettleEvidence)),"internal evidence settlement exposed in editor");
});
var starlightPatch=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText("data/mini-starlight-migration-20261030.json"))!;
System.Text.Json.Nodes.JsonNode StarlightAfter(int id)=>starlightPatch["patches"]!.AsArray().Single(p=>p!["cardId"]!.GetValue<int>()==id)!["abilities"]![0]!["after"]!;
Test("starlight hand-movement conditions accept only own non-draw hand entry",()=>{
 foreach(var id in new[]{1472298,1472406,1472407}){
  var conditions=StarlightAfter(id)["conditions"]!.AsArray().Select(c=>compiler.CompilePredicate(c!["expression"]!.GetValue<string>())).ToList();
  foreach(var row in new[]{(Own:true,Reason:CardMoveReason.AddedToHand,Zone:CardZone.Hand,Expected:true),(Own:true,Reason:CardMoveReason.Drawn,Zone:CardZone.Hand,Expected:false),(Own:false,Reason:CardMoveReason.AddedToHand,Zone:CardZone.Hand,Expected:false),(Own:true,Reason:CardMoveReason.AddedToDeck,Zone:CardZone.Deck,Expected:false)}){
   var owner=row.Own?p1:p2;var moved=factory.CreateAndRegisterNewCard(game,2,owner.PlayerId,CardZone.Staging);var ev=new CardMovedEvent(moved,CardZone.Staging,row.Zone,row.Reason);using var c=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:ev,sourceCard:p1.Leader);
   Check(conditions.All(predicate=>predicate(c))==row.Expected,$"card {id} movement scope is wrong");
  }
 }
});
Test("starlight summon-reason condition excludes played followers and supports ability summons",()=>{
 var predicate=compiler.CompilePredicate(StarlightAfter(1472921)["conditions"]![1]!["expression"]!.GetValue<string>());var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 foreach(var reason in new[]{EventReasonSource.System,EventReasonSource.Ability}){using var c=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:new SummonEvent(p1,card,reasonSource:reason),sourceCard:p1.Leader);Check(predicate(c)==(reason==EventReasonSource.Ability),"wrong summon reason");}
});
Test("starlight training history expressions compile with current-player identity filter",()=>{
 foreach(var id in new[]{1471951,1471952}){var expression=StarlightAfter(id)["conditions"]![0]!["expression"]!.GetValue<string>();Check(expression.Contains("self.CurrentPlayerId == context.owner.playerId"),"owner turn identity missing");compiler.CompilePredicate(expression);}
});
Test("starlight Sato heart captures summoned instance id rather than applying to the source",()=>{
 p1.Field.Clear();var summoned=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);var other=factory.CreateAndRegisterNewCard(game,3,p1.PlayerId,CardZone.Field);var config=StarlightAfter(1472650);var ev=new SummonEvent(p1,summoned);using var summonContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:ev,sourceCard:p1.Leader);
 var captured=compiler.CompileGetter<object>(config["effects"]![0]!["outputs"]!["心随从"]!.GetValue<string>())(new object(),summonContext);Check(captured is Guid guid&&guid==summoned.InstanceId,"summoned instance not captured");
 Check(config["effects"]![0]!["chainedAbilities"]!.AsArray().All(a=>a!["targets"]![0]!["query"]!["sourcePoolExpression"]!.GetValue<string>().Contains("self.instanceId == context.triggeringEvent.Outputs.心随从")),"chain lost captured target");
 var completed=new EffectCompletedEvent(EffectType.None,new Dictionary<string,object>(),new List<TargetInfo>(),new List<IGameEvent>(),new List<CardInstance>(),new List<CardInstanceSnapshot>(),new Dictionary<string,object?>{{"心随从",captured}},true,p1.Leader);
 using var chainContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:completed,sourceCard:p1.Leader);
 var targetExpression=config["effects"]![0]!["chainedAbilities"]![0]!["targets"]![0]!["query"]!["sourcePoolExpression"]!.GetValue<string>();
 var selected=compiler.CompileGetter<object>(targetExpression)(new object(),chainContext);Check(selected is IEnumerable<CardInstance> pool&&pool.Single()==summoned,"chain resolved a different follower");
});
Test("starlight Savior sword activation selects its own amulet",()=>{
 var target=StarlightAfter(1474098)["targets"]!.AsArray().Single()!;var expression=target["query"]!["sourcePoolExpression"]!.GetValue<string>();Check(expression=="context.SourceCard","sword self target missing");
 var getter=compiler.CompileGetter<object>(expression);Check(ReferenceEquals(getter(new object(),context),p1.Leader),"self reference not resolved");
});
Console.WriteLine(JsonSerializer.Serialize(results,options));
return results.Any(r=>((bool)r.GetType().GetProperty("passed")!.GetValue(r)!) == false)?1:0;

public class Proxy<T> : DispatchProxy where T:class {
 public Func<MethodInfo,object?[]?,object?> Handler = null!;
 public static T Make(Func<MethodInfo,object?[]?,object?> handler){var p=Create<T,Proxy<T>>();((Proxy<T>)(object)p).Handler=handler;return p;}
 protected override object? Invoke(MethodInfo? method,object?[]? args){var value=Handler(method!,args);if(value!=null)return value;var type=method!.ReturnType;if(type==typeof(void))return null;if(type==typeof(bool))return false;if(type.IsValueType)return Activator.CreateInstance(type);return null;}
}
