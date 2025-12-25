# Change: Add Event/Effect UI Categories For Ability Editor

## Why
能力编辑器目前把事件/效果平铺在下拉列表中，玩家需要理解系统事件与底层流程才能定位能力触发点，导致编辑效率与理解成本偏高。我们需要按玩家视角对事件与效果分组，并隐藏/折叠 ActionRequests 这类系统请求事件。

## What Changes
- 为事件与效果元数据新增玩家视角分类字段，并提供可见性标记以区分系统/隐藏事件。
- Ability Editor 在事件与效果选择器中按分类分组显示，默认隐藏系统/请求类事件，允许用户显式展开。
- 为现有事件与效果补充分组归类：基础行动、位置变化、临时状态、资源数值、回合流程、特殊事件、系统请求。

## Impact
- Affected specs (new): `ability-editor-metadata`
- Affected code: `Server/Domain/Events/Metadata/*`, `Server/Services/EventMetadataService.cs`, `Server/Domain/Abilities/Effects/Handlers/Parameters/Metadata/EffectMetadataAttribute.cs`, `Server/Services/EffectUIMetadataService.cs`, `Server/Dtos/*`, `Server/Services/AbilityEditorMetadataService.cs`, `src/components/*`
