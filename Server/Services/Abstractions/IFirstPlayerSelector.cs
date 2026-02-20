namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Selects whether Player1 should go first when a new game state is created.
    /// </summary>
    public interface IFirstPlayerSelector
    {
        bool IsPlayer1First();
    }
}
