using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Buffs;

internal static class BuffValueCalculation
{
    public static int Apply(int currentValue, ModifierLayer layer, double value) => layer switch
    {
        // Addition truncates the modifier before adding; other layers round the result upward.
        ModifierLayer.Additive => currentValue + (int)value,
        ModifierLayer.Multiplicative => (int)Math.Ceiling(currentValue * value),
        ModifierLayer.Set or ModifierLayer.Base => (int)Math.Ceiling(value),
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown modifier layer.")
    };
}
