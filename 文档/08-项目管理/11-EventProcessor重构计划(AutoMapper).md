# EventProcessor 重构计划：迁移至 AutoMapper

## 1. 背景与动机

当前，项目中的领域事件到DTO（数据传输对象）的转换逻辑集中在 `Server/Domain/Events/EventProcessor.cs` 文件中的 `ConvertEventToDto` 方法。该方法使用一个庞大的 `switch` 语句来手动处理每一种事件类型。

虽然这种方法在项目初期是可行的，但随着事件类型的不断增加，它暴露了几个严重的架构问题：

*   **违反开闭原则 (OCP)**: 每增加一个新的事件，都必须修改 `EventProcessor.cs` 文件，这使得该文件成为一个高频变更点和潜在的错误源。
*   **可维护性差**: `switch` 语句会变得越来越臃肿，代码难以阅读和维护。
*   **代码冗余**: 大量的转换逻辑是重复的属性赋值，增加了样板代码量。

为了解决这些问题，提升代码质量和可维护性，我们计划将此转换逻辑重构为使用 **AutoMapper** 库。

## 2. 目标

*   将事件到DTO的映射逻辑从 `EventProcessor` 中剥离。
*   使用 AutoMapper 的声明式配置来定义映射规则。
*   使系统在添加新事件时，只需添加映射配置，而无需修改核心处理器代码，从而遵循开闭原则。
*   减少样板代码，提高开发效率。

## 3. 重构方案与步骤

### 步骤 1: 创建并配置 AutoMapper Profile

1.  在 `Server/Mappings/` 目录下创建一个新的 `MappingProfile.cs` 文件。
2.  让 `MappingProfile` 类继承自 `AutoMapper.Profile`。
3.  在构造函数中，使用 `CreateMap<TSource, TDestination>()` 为**所有**现有的事件和DTO添加映射规则。

**示例 (`MappingProfile.cs`):**
```csharp
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // 简单的一对一映射
            CreateMap<MulliganCompletedEvent, MulliganCompletedEventDto>();
            CreateMap<TurnEndEvent, TurnEndedEventDto>();
            // ... 其他简单映射 ...

            // 需要自定义字段的映射
            CreateMap<AttackStateUpdatedEvent, AttackStateUpdatedEventDto>()
                .ForMember(dest => dest.TargetCardInstanceId, opt => opt.MapFrom(src => src.Source.Id));

            // 需要复杂逻辑的映射（见第 4 节）
            CreateMap<DrawEvent, CardDrawnEventDto>()
                .ConstructUsing((src, context) => {
                    var gameContext = context.Items["GameContext"] as GameContext;
                    // ... 实现复杂的转换逻辑 ...
                    return new CardDrawnEventDto(/* ... */);
                });
        }
    }
}
```

### 步骤 2: 修改 `EventProcessor` 以使用 AutoMapper

1.  **移除** `EventProcessor.cs` 中庞大的 `ConvertEventToDto` 方法及其所有私有的 `Convert...` 辅助方法。
2.  创建一个新的、简洁的 `ConvertEventToDto` 方法，该方法将直接调用 AutoMapper。

**修改后的 `EventProcessor.cs`:**
```csharp
public class EventProcessor : IEventProcessor
{
    private readonly IMapper _mapper;
    // ... 其他依赖 ...

    public EventProcessor(IMapper mapper, /*...*/)
    {
        _mapper = mapper;
        // ...
    }

    public async Task<(List<IGameEvent> NewDomainEvents, List<GameEventDto> ResultingDtos, ...)> ProcessEventAsync(IGameEvent initialEvent, GameContext context)
    {
        // ...
        var initialEventDto = ConvertEventToDto(initialEvent, context);
        if (initialEventDto != null)
        {
            resultingDtos.Add(initialEventDto);
        }
        // ...
    }

    private GameEventDto? ConvertEventToDto(IGameEvent gameEvent, GameContext context)
    {
        try
        {
            // 使用 AutoMapper 进行转换
            // 通过 opt.Items 传递 GameContext
            var dto = _mapper.Map<GameEventDto>(gameEvent, opt => {
                opt.Items["GameContext"] = context;
            });
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping event {EventType} to DTO", gameEvent.GetType().Name);
            return new ErrorOccurredEventDto(context.RequestingPlayerId, $"Failed to map event: {ex.Message}");
        }
    }
}
```

### 步骤 3: 配置 `Program.cs`

确保 `Program.cs` (或 `Startup.cs`) 中的 AutoMapper 注册代码能够扫描到包含 `MappingProfile` 的程序集。当前项目已正确配置，无需修改。

```csharp
// Server/Program.cs
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

## 4. 解决难点：处理需要 `GameContext` 的复杂映射

当前 `switch` 方法的一个优点是能够方便地使用 `GameContext`。在使用 AutoMapper 时，我们有几种方式来解决这个问题：

1.  **使用 `ConstructUsing` 和 `Items` 字典 (推荐)**:
    *   在调用 `_mapper.Map` 时，将 `GameContext` 实例存入 `Items` 字典中。
    *   在 `MappingProfile` 中，使用 `.ConstructUsing()` 或 `.AfterMap()` 来访问这个上下文，并执行复杂的转换逻辑。

    **调用端 (`EventProcessor.cs`):**
    ```csharp
    _mapper.Map<GameEventDto>(gameEvent, opt => {
        opt.Items["GameContext"] = context;
    });
    ```

    **配置端 (`MappingProfile.cs`):**
    ```csharp
    CreateMap<DrawEvent, CardDrawnEventDto>()
        .ConstructUsing((src, resolutionContext) => {
            var gameContext = resolutionContext.Items["GameContext"] as GameContext;
            if (gameContext == null) return null;

            var firstCardId = src.DrawnCardInstanceIds.FirstOrDefault();
            var cardInstance = gameContext.CurrentGameState.FindCardInstanceById(firstCardId);
            // ...
            return new CardDrawnEventDto(/* ... */);
        });
    ```

2.  **自定义 `ITypeConverter`**: 对于极其复杂的转换，可以创建一个实现了 `ITypeConverter<TSource, TDestination>` 的类。这种方法提供了最大的灵活性，但通常只在必要时使用。

## 5. 实施计划

建议将此重构作为一个独立的技术债偿还任务，在当前BUG修复周期之后进行。可以分阶段实施：

1.  **阶段一**: 创建 `MappingProfile.cs`，并迁移所有**简单**的、不需要 `GameContext` 的事件映射。
2.  **阶段二**: 使用推荐的 `Items` 字典方法，迁移所有需要 `GameContext` 的复杂事件映射。
3.  **阶段三**: 完全移除 `EventProcessor` 中的旧 `switch` 逻辑，并进行全面的回归测试。

通过此重构，项目的事件处理系统将变得更加健壮、可维护和可扩展。