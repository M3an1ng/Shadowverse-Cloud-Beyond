using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Extensions;
using ShadowverseBeyond.Server.Services.Abstractions;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// 基础解析器，提供通用的CardInstance集合映射功能
    /// </summary>
    public abstract class BaseCardInstanceCollectionResolver : IValueResolver<PlayerState, PlayerStateDto, List<CardInstanceDto>>
    {
        protected readonly IGameDataProvider _gameDataProvider;
        protected readonly IMapper _mapper;

        protected BaseCardInstanceCollectionResolver(IGameDataProvider gameDataProvider, IMapper mapper)
        {
            _gameDataProvider = gameDataProvider;
            _mapper = mapper;
        }

        public abstract List<CardInstanceDto> Resolve(PlayerState source, PlayerStateDto destination, List<CardInstanceDto> destMember, ResolutionContext context);

        protected List<CardInstanceDto> MapCollection(List<CardInstance> sourceCollection)
        {
            return sourceCollection.ToCompleteDtos(_gameDataProvider, _mapper);
        }
    }

    /// <summary>
    /// 专门用于Hand集合的解析器
    /// </summary>
    public class HandCollectionResolver : BaseCardInstanceCollectionResolver
    {
        public HandCollectionResolver(IGameDataProvider gameDataProvider, IMapper mapper)
            : base(gameDataProvider, mapper)
        {
        }

        public override List<CardInstanceDto> Resolve(PlayerState source, PlayerStateDto destination, List<CardInstanceDto> destMember, ResolutionContext context)
        {
            return MapCollection(source.Hand);
        }
    }

    /// <summary>
    /// 专门用于Field集合的解析器
    /// </summary>
    public class FieldCollectionResolver : BaseCardInstanceCollectionResolver
    {
        public FieldCollectionResolver(IGameDataProvider gameDataProvider, IMapper mapper)
            : base(gameDataProvider, mapper)
        {
        }

        public override List<CardInstanceDto> Resolve(PlayerState source, PlayerStateDto destination, List<CardInstanceDto> destMember, ResolutionContext context)
        {
            return MapCollection(source.Field);
        }
    }

    /// <summary>
    /// 专门用于Graveyard集合的解析器
    /// </summary>
    public class GraveyardCollectionResolver : BaseCardInstanceCollectionResolver
    {
        public GraveyardCollectionResolver(IGameDataProvider gameDataProvider, IMapper mapper)
            : base(gameDataProvider, mapper)
        {
        }

        public override List<CardInstanceDto> Resolve(PlayerState source, PlayerStateDto destination, List<CardInstanceDto> destMember, ResolutionContext context)
        {
            return MapCollection(source.Graveyard);
        }
    }

    /// <summary>
    /// 专门用于Extra集合的解析器
    /// </summary>
    public class ExtraCollectionResolver : BaseCardInstanceCollectionResolver
    {
        public ExtraCollectionResolver(IGameDataProvider gameDataProvider, IMapper mapper)
            : base(gameDataProvider, mapper)
        {
        }

        public override List<CardInstanceDto> Resolve(PlayerState source, PlayerStateDto destination, List<CardInstanceDto> destMember, ResolutionContext context)
        {
            return MapCollection(source.Extra);
        }
    }

    /// <summary>
    /// 专门用于Leader卡牌的解析器
    /// </summary>
    public class LeaderCardResolver : IValueResolver<PlayerState, PlayerStateDto, CardInstanceDto?>
    {
        private readonly IGameDataProvider _gameDataProvider;
        private readonly IMapper _mapper;

        public LeaderCardResolver(IGameDataProvider gameDataProvider, IMapper mapper)
        {
            _gameDataProvider = gameDataProvider;
            _mapper = mapper;
        }

        public CardInstanceDto? Resolve(PlayerState source, PlayerStateDto destination, CardInstanceDto? destMember, ResolutionContext context)
        {
            return source.Leader?.ToCompleteDto(_gameDataProvider, _mapper);
        }
    }
}
