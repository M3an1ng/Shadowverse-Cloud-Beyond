namespace ShadowverseBeyond.Server.Dtos;
public sealed record AlternatePlayReferenceDto
{
    public int Id { get; init; }
    public string Label { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Cost { get; init; }
    public int? Attack { get; init; }
    public int? Health { get; init; }
    public string ArtPreview { get; init; } = "";
}
