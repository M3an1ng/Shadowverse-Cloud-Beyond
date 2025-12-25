namespace ShadowverseBeyond.Server.Services;

public readonly record struct DeckOwner(Guid? UserId, Guid? GuestId)
{
    public bool IsGuest => !UserId.HasValue || UserId.Value == Guid.Empty;
    public bool IsUser => !IsGuest;

    public Guid? GetUserIdOrNull() => IsUser ? UserId : null;
    public Guid? GetGuestIdOrNull() => IsGuest ? GuestId : null;
}
