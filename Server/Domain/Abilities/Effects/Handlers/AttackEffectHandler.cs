using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Exceptions;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 攻击效果处理器
    /// 处理单位攻击操作，包括攻击单位和攻击玩家
    /// 遵循统一架构：Handler负责验证和事件生成，领域实体负责状态修改
    /// </summary>
    public class AttackEffectHandler : EffectHandlerBase<AttackEffectParams>
    {
        private readonly IMapper _mapper;

        public AttackEffectHandler(
            ILogger logger,
            IMapper mapper)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// 执行攻击效果
        /// </summary>
        public override async Task<GameStateActionResult> ApplyAsync(
            GameContext context,
            AttackEffectParams parameters)
        {
            Logger.LogDebug("[AttackEffectHandler] Starting attack execution for attacker {AttackerId} targeting {TargetId}",
                parameters.AttackerInstanceId, parameters.TargetId);

            var events = new List<IGameEvent>();

            // 1. 获取攻击者
            var attacker = FindAttackerInGame(context.CurrentGameState, parameters.AttackerInstanceId);
            if (attacker == null)
            {
                throw new GameLogicException($"Attacker not found: {parameters.AttackerInstanceId}");
            }

            // 2. 获取攻击者所属玩家
            var attackerPlayer = GetPlayerByCardInstance(context.CurrentGameState, attacker);
            if (attackerPlayer == null)
            {
                throw new GameLogicException($"Cannot find player for attacker: {parameters.AttackerInstanceId}");
            }

            // 3. 验证攻击前置条件
            ValidateAttackPreconditions(context.CurrentGameState, attackerPlayer, attacker, parameters);

            // 4. 根据目标类型执行攻击
            List<IGameEvent> attackEvents;
            if (parameters.TargetType == AttackTargetType.Player)
            {
                // 攻击玩家
                var targetPlayer = GetPlayerById(context.CurrentGameState, parameters.TargetId);
                if (targetPlayer == null)
                {
                    throw new GameLogicException($"Target player not found: {parameters.TargetId}");
                }

                attackEvents = attacker.AttackPlayer(targetPlayer);
            }
            else
            {
                // 攻击单位
                var targetUnit = FindCardInstanceInGame(context.CurrentGameState, parameters.TargetId);
                if (targetUnit == null)
                {
                    throw new GameLogicException($"Target unit not found: {parameters.TargetId}");
                }

                attackEvents = attacker.PerformAttack(targetUnit);
            }

            events.AddRange(attackEvents);

            // 5. 生成攻击事件
            var playerDto = _mapper.Map<PlayerStateDto>(attackerPlayer);
            var attackEvent = new AttackEvent(playerDto, attacker, parameters.TargetId, parameters.TargetType);
            events.Add(attackEvent);

            await Task.CompletedTask; // 攻击逻辑是同步的

            return new GameStateActionResult
            {
                Success = true,
                GeneratedEvents = events,
                ErrorMessage = $"Attack executed successfully."
            };
        }

        /// <summary>
        /// 验证攻击前置条件
        /// </summary>
        private static void ValidateAttackPreconditions(
            GameState gameState,
            PlayerState attackerPlayer,
            CardInstance attacker,
            AttackEffectParams parameters)
        {
            if (!parameters.ForceAttack)
            {
                // 游戏状态验证
                if (gameState.IsGameOver)
                    throw new GameLogicException("Game is already over.");

                if (gameState.CurrentPlayerId != attackerPlayer.PlayerId)
                    throw new GameLogicException($"It's not player {attackerPlayer.PlayerId}'s turn.");

                // 攻击者验证
                if (!attacker.CanPerformAttack())
                {
                    var reason = GetAttackFailureReason(attacker);
                    throw new GameLogicException($"Unit cannot attack: {reason}");
                }

                // 区域验证
                if (attacker.CurrentZone != CardZone.Field)
                    throw new GameLogicException("Only units on the field can attack.");
            }
        }

        /// <summary>
        /// 获取攻击失败的具体原因
        /// </summary>
        private static string GetAttackFailureReason(CardInstance attacker)
        {
            if (!attacker.CanAttack)
                return "Unit cannot attack this turn";

            if (attacker.CurrentAttack <= 0)
                return "Unit has no attack power";

            var maxAttacks = 1; // 默认每回合1次攻击
            if (attacker.AttacksThisTurn >= maxAttacks)
                return "Unit has already attacked this turn";

            return "Unknown reason";
        }

        /// <summary>
        /// 在游戏状态中查找攻击者
        /// </summary>
        private static CardInstance? FindAttackerInGame(GameState gameState, Guid attackerId)
        {
            // 在两个玩家的场地中查找
            var attacker = gameState.Player1.Field.Find(c => c.InstanceId == attackerId);
            if (attacker != null) return attacker;

            attacker = gameState.Player2.Field.Find(c => c.InstanceId == attackerId);
            return attacker;
        }

        /// <summary>
        /// 在游戏状态中查找卡牌实例
        /// </summary>
        private static CardInstance? FindCardInstanceInGame(GameState gameState, Guid cardId)
        {
            // 在所有区域中查找目标
            var allCards = new List<CardInstance>();
            allCards.AddRange(gameState.Player1.Field);
            allCards.AddRange(gameState.Player1.Hand);
            allCards.AddRange(gameState.Player2.Field);
            allCards.AddRange(gameState.Player2.Hand);
            allCards.Add(gameState.Player1.Leader);
            allCards.Add(gameState.Player2.Leader);

            return allCards.Find(c => c.InstanceId == cardId);
        }

        /// <summary>
        /// 根据卡牌实例获取所属玩家
        /// </summary>
        private static PlayerState? GetPlayerByCardInstance(GameState gameState, CardInstance card)
        {
            if (card.OwnerPlayerId == gameState.Player1.PlayerId)
                return gameState.Player1;

            if (card.OwnerPlayerId == gameState.Player2.PlayerId)
                return gameState.Player2;

            return null;
        }

        /// <summary>
        /// 根据玩家ID获取玩家状态
        /// </summary>
        private static PlayerState? GetPlayerById(GameState gameState, Guid playerId)
        {
            if (playerId == gameState.Player1.PlayerId)
                return gameState.Player1;

            if (playerId == gameState.Player2.PlayerId)
                return gameState.Player2;

            return null;
        }
    }

    /// <summary>
    /// 攻击事件
    /// </summary>
    public class AttackEvent : GameEventBase
    {
        public PlayerStateDto AttackerPlayer { get; }
        public CardInstance Attacker { get; }
        public Guid TargetId { get; }
        public AttackTargetType TargetType { get; }

        public AttackEvent(PlayerStateDto attackerPlayer, CardInstance attacker, Guid targetId, AttackTargetType targetType)
            : base("attack")
        {
            AttackerPlayer = attackerPlayer;
            Attacker = attacker;
            TargetId = targetId;
            TargetType = targetType;

            SourcePlayer = attackerPlayer;
            SourceCard = attacker;
            Parameters["targetId"] = targetId;
            Parameters["targetType"] = targetType;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // 攻击事件的执行逻辑已经在Handler中完成
            // 这里只是事件记录，不需要额外的执行逻辑
            await Task.CompletedTask;
            return new List<IGameEvent>();
        }
    }
}
