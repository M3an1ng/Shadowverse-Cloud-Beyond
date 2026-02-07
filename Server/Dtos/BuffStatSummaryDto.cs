namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Aggregated stat changes from buffs for UI display.
    /// </summary>
    public record BuffStatSummaryDto
    {
        public int AttackDelta { get; init; }
        public int HealthDelta { get; init; }
        public int CostDelta { get; init; }
    }
}
