using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;
namespace ShadowverseBeyond.Server.Services;
public sealed record TakeTwoPool(string[] Packs, int[] IncludedIds, int[] ExcludedIds, Dictionary<string, int> Redraws)
{
    public Dictionary<string, int[]> NeutralByClass { get; init; } = new();
}
public sealed class TakeTwoSeat
{
    public Guid PlayerId { get; init; }
    public Guid? LocalTestOwnerId { get; set; }
    public string Token { get; init; } = Guid.NewGuid().ToString("N"); public string? Class { get; set; }
    public List<int> Cards { get; set; } = new(); public int Round { get; set; }
    public int Redraws { get; set; }
    public bool Ready { get; set; }
    public int Sequence { get; set; }
    public Dictionary<string, TakeTwoPlan> Plans { get; init; } = new();
}
public sealed record TakeTwoRound(int[][] Offer, List<int[][]> RedrawOffers, int RedrawIndex = 0);
public sealed record TakeTwoPlan(int[] Core, List<TakeTwoRound> Rounds, int Redraws);
public sealed class TakeTwoRoom
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N"); public string Code { get; init; } = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); public string Environment { get; set; } = "rotation"; public TakeTwoPool Pool { get; set; } = new([], [], [], new()); public List<CardDefinition> Catalog { get; set; } = new(); public List<TakeTwoSeat> Players { get; init; } = new(); public DateTime Expires { get; set; } = DateTime.UtcNow.Add(RoomLifetime.DefaultTtl); public bool Closed { get; set; }
    public MatchRoom? Match { get; set; }
}
public sealed class TakeTwoService(IMatchService matches, IGameService? games = null)
{
    private readonly ConcurrentDictionary<string, TakeTwoRoom> rooms = new();
    private readonly object membershipGate = new();
    private bool IsActive(TakeTwoRoom room)
    {
        lock (room)
        {
            if (room.Closed) return false;
            if (room.Match == null)
            {
                if (room.Expires > DateTime.UtcNow) return true;
                MarkClosed(room); return false;
            }
            // MatchService returns snapshots: always consult the live room after handoff.
            var current = matches.Get(room.Match.RoomId);
            if (!current.Success || current.Room == null || current.Room.Status != MatchStatus.Started ||
                games?.GetGameState(current.Room.ActiveGameId ?? "")?.IsGameOver == true)
            { MarkClosed(room); return false; }
            room.Expires = current.Room.ExpiresAt;
            return true;
        }
    }
    private static void MarkClosed(TakeTwoRoom room)
    {
        if (room.Closed) return;
        room.Closed = true;
        room.Expires = DateTime.UtcNow.Add(RoomLifetime.ClosedRetention);
    }
    public void CleanupExpired()
    {
        foreach (var (id, room) in rooms)
        {
            lock (room)
            {
                IsActive(room);
                if (room.Closed && room.Expires <= DateTime.UtcNow) rooms.TryRemove(id, out _);
            }
        }
    }
    private static readonly CardRarity[] rarities = [CardRarity.Bronze, CardRarity.Silver, CardRarity.Bronze, CardRarity.Silver, CardRarity.Bronze, CardRarity.Gold, CardRarity.Bronze, CardRarity.Silver, CardRarity.Bronze, CardRarity.Silver, CardRarity.Bronze, CardRarity.Silver, CardRarity.Bronze, CardRarity.Legend];
    public static TakeTwoPool EmptyPool => new([], [], [], new());
    public static List<CardDefinition> PoolCards(List<CardDefinition> all, string environment, TakeTwoPool pool)
    {
        var valid = all.Where(c => !c.IsMirageCard && c.IsCollectible && CardStyleCatalog.IsCardImplemented(c.Id) && c.Type is CardType.Follower or CardType.Spell or CardType.Amulet).ToList();
        return valid.Where(c => !pool.ExcludedIds.Contains(c.Id) && (pool.IncludedIds.Contains(c.Id) || environment == "as2p" && pool.NeutralByClass.Values.Any(ids => ids.Contains(c.Id)) || ((pool.Packs.Length == 0 ? environment != "as2p" && (environment != "rotation" || c.CardSetDefinition?.Environments.Contains(DeckEnvironment.Rotation) == true) : pool.Packs.Contains(c.CardSet))))).ToList();
    }
    private static int[] Choose(List<CardDefinition> source, int count, HashSet<int>? excluded = null)
    {
        var candidates = source.Where(c => excluded == null || !excluded.Contains(c.Id)).Select(c => c.Id).Distinct().ToList(); if (candidates.Count < count) throw new InvalidOperationException("卡池不足，无法生成互不重复的候选卡"); for (var i = 0; i < count; i++) { var j = RandomNumberGenerator.GetInt32(i, candidates.Count); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
        return candidates.Take(count).ToArray();
    }
    public static List<CardDefinition> ClassPool(List<CardDefinition> catalog, string environment, TakeTwoPool settings, CardClass cls) => catalog.Where(c =>
        c.Class == cls || c.Class == CardClass.Neutral &&
        (environment != "as2p" || !settings.NeutralByClass.Values.Any(ids => ids.Contains(c.Id)) ||
         settings.NeutralByClass.GetValueOrDefault(cls.ToString(), []).Contains(c.Id))).ToList();

    private static int[] Offer(List<CardDefinition> choices, bool requireLegend)
    {
        if (!requireLegend) return Choose(choices, 4);
        var first = Choose(choices.Where(c => c.Rarity == CardRarity.Legend).ToList(), 1);
        var offer = first.Concat(Choose(choices, 3, first.ToHashSet())).ToArray();
        RandomNumberGenerator.Shuffle(offer.AsSpan());
        return offer;
    }

    public static TakeTwoSeat Prepare(Guid id, List<CardDefinition> catalog, TakeTwoPool settings, string environment = "rotation")
    {
        var seat = new TakeTwoSeat { PlayerId = id };
        foreach (var cls in Enum.GetValues<CardClass>().Where(c => c != CardClass.Neutral)) try
        {
            var own = catalog.Where(c => c.Class == cls).ToList();
            var combined = ClassPool(catalog, environment, settings, cls);
            var first = Choose(own.Where(c => c.Rarity == CardRarity.Legend).ToList(), 1);
            var second = Choose(own.Where(c => c.Rarity is CardRarity.Gold or CardRarity.Legend).ToList(), 1, first.ToHashSet());
            var redraws = settings.Redraws.GetValueOrDefault(cls.ToString(), 2);
            var rounds = new List<TakeTwoRound>();
            for (var index = 0; index < rarities.Length; index++)
            {
                var special = environment == "as2p" && index is 6 or 13;
                var rarity = rarities[index];
                var choices = environment == "as2p"
                    ? combined.Where(c => !special || c.Rarity is CardRarity.Gold or CardRarity.Legend).ToList()
                    : combined.Where(c => rarity == CardRarity.Legend ? c.Rarity is CardRarity.Gold or CardRarity.Legend : c.Rarity == rarity).ToList();
                var four = Offer(choices, special);
                var offer = new[] { four[..2], four[2..] };
                var redrawOffers = new List<int[][]>();
                for (var r = 0; r < redraws; r++)
                {
                    // Redraw ignores the round's rarity rules. Exclude only the currently
                    // displayed four; earlier offers and already drafted cards remain eligible.
                    var next = Choose(combined, 4, four.ToHashSet());
                    redrawOffers.Add(new[] { next[..2], next[2..] });
                    four = next;
                }
                rounds.Add(new(offer, redrawOffers));
            }
            seat.Plans[cls.ToString()] = new(first.Concat(second).ToArray(), rounds, redraws);
        }
        catch (InvalidOperationException) { /* Incomplete classes are unavailable without weakening offer rules. */ }
        if (seat.Plans.Count == 0) throw new InvalidOperationException("此卡池没有满足双选要求的职业，请补充卡片或减少重抽次数");
        return seat;
    }
    private string NextUniqueCode()
    {
        for (var i = 0; i < 32; i++)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            if (!rooms.Values.Any(r => !r.Closed && r.Code == code)) return code;
        }
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }
    public TakeTwoRoom Create(Guid owner, string environment, TakeTwoPool pool, List<CardDefinition> all)
    {
        lock (membershipGate)
        {
            CleanupExpired();
            // Capture mutable settings so subsequent edits cannot change either player's room.
            var snapshot = JsonSerializer.Deserialize<TakeTwoPool>(JsonSerializer.Serialize(pool))!;
            var catalog = PoolCards(all, environment, snapshot);
            var room = new TakeTwoRoom { Code = NextUniqueCode(), Environment = environment, Pool = snapshot, Catalog = catalog };
            room.Players.Add(Prepare(owner, catalog, snapshot, environment));
            rooms[room.Id] = room;
            return room;
        }
    }
    public TakeTwoRoom Find(string id)
    {
        CleanupExpired();
        return rooms.TryGetValue(id, out var room) ? room : throw new InvalidOperationException("房间不存在或已过期");
    }
    public TakeTwoRoom Join(string code, Guid owner, string? roomId = null)
    {
        lock (membershipGate)
        {
            CleanupExpired();
            TakeTwoRoom? room = null;
            if (!string.IsNullOrWhiteSpace(roomId))
            {
                var normalizedId = roomId.Trim().Replace("-", "");
                if (rooms.TryGetValue(normalizedId, out var byId) && IsActive(byId))
                {
                    if (!string.Equals(byId.Code, code?.Trim(), StringComparison.Ordinal))
                        throw new InvalidOperationException("邀请码不正确");
                    room = byId;
                }
            }
            else
            {
                room = rooms.Values.FirstOrDefault(r => r.Code == code?.Trim() && IsActive(r));
            }
            if (room == null) throw new InvalidOperationException("房间不存在");
            lock (room)
            {
                if (!IsActive(room)) throw new InvalidOperationException("房间已结束");
                if (room.Players.Any(p => p.PlayerId == owner)) return room;
                if (room.Players.Count >= 2) throw new InvalidOperationException("房间已满");
                room.Players.Add(Prepare(owner, room.Catalog, room.Pool, room.Environment));
                room.Expires = DateTime.UtcNow.Add(RoomLifetime.DefaultTtl);
                return room;
            }
        }
    }
    public void UpdatePool(TakeTwoRoom room, TakeTwoSeat host, string environment, TakeTwoPool pool, List<CardDefinition> all)
    {
        lock (room)
        {
            if (host != room.Players[0]) throw new UnauthorizedAccessException();
            if (room.Closed || room.Match != null || room.Players.Any(p => p.Class != null))
                throw new InvalidOperationException("已开始选牌，不能更换本局卡池");
            var snapshot = JsonSerializer.Deserialize<TakeTwoPool>(JsonSerializer.Serialize(pool))!;
            var catalog = PoolCards(all, environment, snapshot);
            var plans = room.Players.Select(p => Prepare(p.PlayerId, catalog, snapshot, environment)).ToArray();
            room.Environment = environment; room.Pool = snapshot; room.Catalog = catalog;
            for (var i = 0; i < plans.Length; i++) {
                room.Players[i].Plans.Clear();
                foreach (var plan in plans[i].Plans) room.Players[i].Plans[plan.Key] = plan.Value;
            }
        }
    }
    public void LeaveCurrent(Guid owner)
    {
        lock (membershipGate)
        {
            var current = rooms.Values.OrderByDescending(r => r.Expires).FirstOrDefault(r => {
                lock (r) return !r.Closed && r.Players.Any(p => p.PlayerId == owner || p.LocalTestOwnerId == owner);
            });
            if (current != null) Leave(current);
        }
    }
    public void Leave(TakeTwoRoom room)
    {
        lock (room)
        {
            if (room.Closed) return;
            MarkClosed(room);
            if (room.Match != null) matches.Close(room.Match.RoomId, "对方退出了房间");
        }
    }
    public TakeTwoSeat JoinLocalTest(TakeTwoRoom room, Guid owner, string hostToken)
    {
        lock (room)
        {
            var host = Authorize(room, owner, hostToken);
            if (host != room.Players[0]) throw new UnauthorizedAccessException();
            if (room.Closed || room.Match != null) throw new InvalidOperationException("房间已结束或已开始对战");
            var existing = room.Players.FirstOrDefault(p => p.LocalTestOwnerId == owner);
            if (existing != null) return existing;
            if (room.Players.Count != 1) throw new InvalidOperationException("已有对手加入，不能开启本机对战");
            var guest = Prepare(Guid.NewGuid(), room.Catalog, room.Pool, room.Environment);
            guest.LocalTestOwnerId = owner;
            room.Players.Add(guest);
            return guest;
        }
    }
    public TakeTwoSeat Authorize(TakeTwoRoom room, Guid player, string token)
    {
        lock (room)
        {
            var seat = room.Players.SingleOrDefault(p => (p.PlayerId == player || p.LocalTestOwnerId == player) && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(p.Token), System.Text.Encoding.UTF8.GetBytes(token))) ?? throw new UnauthorizedAccessException();
            // Authenticated participant polling is the draft heartbeat; spectators never touch TTL.
            if (IsActive(room) && room.Match == null) room.Expires = DateTime.UtcNow.Add(RoomLifetime.DefaultTtl);
            return seat;
        }
    }
    public void Act(TakeTwoRoom room, TakeTwoSeat seat, string action, int sequence, string? cls, int side)
    {
        lock (room)
        {
            if (action == "leave") { Leave(room); return; }
            if (room.Closed) throw new InvalidOperationException("对方退出了房间"); if (sequence <= seat.Sequence) return; if (sequence != seat.Sequence + 1) throw new InvalidOperationException("选择顺序已变化，请刷新当前选牌");
            if (action != "leave" && room.Players.Count != 2) throw new InvalidOperationException("等待对手加入后才能开始双选");
            if (action == "class") { if (seat.Class != null || cls == null || !seat.Plans.TryGetValue(cls, out var plan)) throw new InvalidOperationException("职业不可选"); seat.Class = cls; seat.Cards.AddRange(plan.Core); seat.Redraws = plan.Redraws; seat.Round = 1; }
            else if (action == "pick") { if (seat.Class == null || seat.Round < 1 || seat.Round > 14 || side < 0 || side > 1) throw new InvalidOperationException("当前不能选择卡牌"); seat.Cards.AddRange(seat.Plans[seat.Class].Rounds[seat.Round - 1].Offer[side]); seat.Round++; }
            else if (action == "redraw") { if (seat.Class == null || seat.Round < 1 || seat.Round > 14 || seat.Redraws <= 0) throw new InvalidOperationException("当前不能重抽"); var plan = seat.Plans[seat.Class]; var round = plan.Rounds[seat.Round - 1]; var index = round.RedrawIndex; plan.Rounds[seat.Round - 1] = round with { Offer = round.RedrawOffers[index], RedrawIndex = index + 1 }; seat.Redraws--; }
            else if (action == "ready") { if (seat.Cards.Count != 30) throw new InvalidOperationException("请先完成30张选牌"); seat.Ready = true; if (room.Players.Count == 2 && room.Players.All(p => p.Ready)) Start(room); }
            else throw new InvalidOperationException("不支持的操作"); seat.Sequence = sequence;
            if (room.Match == null) room.Expires = DateTime.UtcNow.Add(RoomLifetime.DefaultTtl);
        }
    }
    private void Start(TakeTwoRoom room)
    {
        if (room.Match != null) return; var host = room.Players[0]; var guest = room.Players[1]; var created = matches.Create(host.PlayerId); if (!created.Success) throw new InvalidOperationException(created.Message); var native = created.Room!; var joined = matches.TryJoin(native.RoomId, native.JoinCode, guest.PlayerId); if (!joined.Success) throw new InvalidOperationException(joined.Message);
        foreach (var p in room.Players) { var result = matches.SubmitDeckSelection(native.RoomId, p.PlayerId, new MatchDeckSelection { UserId = p.LocalTestOwnerId ?? p.PlayerId, GeneratedDraftCardIds = p.Cards.ToArray(), DraftEnvironment = room.Environment, DeckClass = Enum.Parse<CardClass>(p.Class!), Environment = DeckEnvironment.Open, Rule = DeckRule.Standard }); if (!result.Success) throw new InvalidOperationException(result.Message); matches.MarkReady(native.RoomId, p.PlayerId); }
        var started = matches.Start(native.RoomId, host.PlayerId); if (!started.Success) throw new InvalidOperationException(started.Message); room.Match = matches.Get(native.RoomId).Room;
    }
    public sealed record SpectatorSeat(string Role, string? Class, int[] Cards, int Round, bool Ready) { [System.Text.Json.Serialization.JsonIgnore] public Guid PlayerId { get; init; } public string? Username { get; init; } }
    public sealed record SpectatorRoom(string RoomId, string Environment, string Status, bool Closed, string? MatchRoomId, SpectatorSeat[] Players);
    public SpectatorRoom SpectatorView(TakeTwoRoom room)
    {
        lock (room)
        {
            IsActive(room);
            return new(room.Id, room.Environment,
                room.Closed ? "Closed" : room.Match != null ? "Started" : room.Players.Count < 2 ? "Waiting" : "Drafting",
                room.Closed, room.Match?.RoomId,
                room.Players.Select((p, i) => new SpectatorSeat(i == 0 ? "host" : "guest", p.Class, p.Cards.ToArray(), p.Round, p.Ready) { PlayerId = p.PlayerId }).ToArray());
        }
    }
    public SpectatorRoom[] SpectatorRooms() { CleanupExpired(); return rooms.Values.Where(IsActive).Select(SpectatorView).Where(r => !r.Closed).ToArray(); }
    public object View(TakeTwoRoom room, TakeTwoSeat seat, bool includePlans = true) { lock (room) { IsActive(room); var enemy = room.Players.FirstOrDefault(p => p != seat); object? handshake = null; if (room.Match != null) { var host = room.Players[0] == seat; handshake = new { gameId = room.Match.RoomId, playerId = seat.PlayerId, token = host ? room.Match.HostToken : room.Match.GuestToken, joinCode = room.Match.JoinCode, role = host ? "host" : "guest", useAiFallback = false }; } var cardsSnapshot = seat.Cards.ToArray(); var plansSnapshot = includePlans ? seat.Plans.ToDictionary(kv => kv.Key, kv => kv.Value with { Rounds = kv.Value.Rounds.ToList() }) : null; return new { room.Id, room.Code, room.Environment, room.Closed, role = room.Players[0] == seat ? "host" : "guest", packs = room.Pool.Packs, player = new { seat.PlayerId, seat.Token, seat.Class, Cards = cardsSnapshot, seat.Round, seat.Redraws, seat.Ready, seat.Sequence, plans = plansSnapshot }, opponentCount = enemy?.Cards.Count ?? 0, opponentReady = enemy?.Ready ?? false, opponentJoined = enemy != null, handshake }; } }
}
