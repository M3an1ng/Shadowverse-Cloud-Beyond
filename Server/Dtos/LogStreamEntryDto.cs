namespace ShadowverseBeyond.Server.Dtos;

public record LogStreamEntryDto(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? Exception,
    string? Source
);
