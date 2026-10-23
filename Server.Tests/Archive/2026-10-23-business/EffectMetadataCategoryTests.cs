using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Services;
using System;
using System.Linq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EffectMetadataCategoryTests
    {
        [Fact]
        public void Effect_metadata_exposes_category()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var svc = new EffectUIMetadataService(registry);

            var damage = svc.GetAllEffectUIMetadata().First(m => m.EffectTypeName == EffectType.Damage.ToString());

            Assert.Equal(AbilityEditorCategories.BaseActionId, damage.CategoryId);
            Assert.Equal(AbilityEditorCategories.BaseActionLabel, damage.CategoryLabel);
        }

        [Fact]
        public void Tuck_metadata_exposes_deck_insert_mode_dropdown()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var svc = new EffectUIMetadataService(registry);

            var tuck = svc.GetAllEffectUIMetadata().First(m => m.EffectTypeName == EffectType.Tuck.ToString());
            var insertMode = tuck.Parameters.Single(p => p.PropertyName == "InsertMode");

            Assert.Equal(UiControlType.Dropdown, insertMode.ControlType);
            Assert.Equal(nameof(DeckInsertMode), insertMode.EnumName);
            Assert.Equal(DeckInsertMode.Random, insertMode.DefaultValue);
        }

        [Fact]
        public void AddAura_metadata_is_hidden_from_effect_picker()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var svc = new EffectUIMetadataService(registry);

            var effectNames = svc.GetAllEffectUIMetadata().Select(m => m.EffectTypeName).ToList();

            Assert.DoesNotContain(EffectType.AddAura.ToString(), effectNames);
        }

        [Fact]
        public void EffectType_numeric_values_are_unique()
        {
            var duplicateValues = Enum.GetValues<EffectType>()
                .Select(effectType => (Name: effectType.ToString(), Value: (int)effectType))
                .GroupBy(entry => entry.Value)
                .Where(group => group.Count() > 1)
                .ToList();

            Assert.Empty(duplicateValues);
        }
    }
}
