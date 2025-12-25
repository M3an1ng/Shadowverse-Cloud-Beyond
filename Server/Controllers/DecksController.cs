using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecksController : ControllerBase
{
    private readonly IDeckService _deckService;

    public DecksController(IDeckService deckService)
    {
        _deckService = deckService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeckSummaryDto>>> List([FromQuery] Guid? userId, [FromQuery] Guid? guestId, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var decks = await _deckService.ListAsync(owner, cancellationToken);
        return Ok(decks);
    }

    [HttpPost("bootstrap")]
    public async Task<ActionResult<IReadOnlyList<DeckSummaryDto>>> Bootstrap([FromQuery] Guid? userId, [FromQuery] Guid? guestId, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var ensured = await _deckService.EnsureDefaultDeckAsync(owner, cancellationToken);
        if (!ensured.Success)
        {
            return BadRequest(ensured.Error ?? "Failed to create default deck.");
        }

        var decks = await _deckService.ListAsync(owner, cancellationToken);
        return Ok(decks);
    }

    [HttpGet("availability")]
    public async Task<ActionResult<DeckAvailabilityDto>> Availability([FromQuery] Guid? userId, [FromQuery] Guid? guestId, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var hasDeck = await _deckService.HasAnyDeckAsync(owner, cancellationToken);
        return Ok(new DeckAvailabilityDto { HasDeck = hasDeck });
    }

    [HttpGet("{deckId:guid}")]
    public async Task<ActionResult<DeckDetailDto>> Get(Guid deckId, [FromQuery] Guid? userId, [FromQuery] Guid? guestId, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var deck = await _deckService.GetAsync(owner, deckId, cancellationToken);
        return deck == null ? NotFound() : Ok(deck);
    }

    [HttpPost]
    public async Task<ActionResult<DeckDetailDto>> Create([FromQuery] Guid? userId, [FromQuery] Guid? guestId, [FromBody] CreateDeckRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var result = await _deckService.CreateAsync(owner, request, cancellationToken);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(result.Error ?? "Failed to create deck.");
        }

        return CreatedAtAction(nameof(Get), new { deckId = result.Data.DeckId, userId, guestId }, result.Data);
    }

    [HttpPut("{deckId:guid}")]
    public async Task<ActionResult<DeckDetailDto>> Update(Guid deckId, [FromQuery] Guid? userId, [FromQuery] Guid? guestId, [FromBody] UpdateDeckRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var result = await _deckService.UpdateAsync(owner, deckId, request, cancellationToken);
        if (!result.Success || result.Data == null)
        {
            if (IsNotFound(result.Error))
            {
                return NotFound(result.Error);
            }

            return BadRequest(result.Error ?? "Failed to update deck.");
        }

        return Ok(result.Data);
    }

    [HttpDelete("{deckId:guid}")]
    public async Task<IActionResult> Delete(Guid deckId, [FromQuery] Guid? userId, [FromQuery] Guid? guestId, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var result = await _deckService.DeleteAsync(owner, deckId, cancellationToken);
        if (!result.Success)
        {
            return IsNotFound(result.Error) ? NotFound(result.Error) : BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpGet("{deckId:guid}/code")]
    public async Task<ActionResult<object>> ExportCode(Guid deckId, [FromQuery] Guid? userId, [FromQuery] Guid? guestId, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var result = await _deckService.ExportCodeAsync(owner, deckId, cancellationToken);
        if (!result.Success || result.Data == null)
        {
            return IsNotFound(result.Error) ? NotFound(result.Error) : BadRequest(result.Error);
        }

        return Ok(new { code = result.Data });
    }

    [HttpPost("import")]
    public async Task<ActionResult<DeckDetailDto>> ImportCode([FromQuery] Guid? userId, [FromQuery] Guid? guestId, [FromBody] ImportDeckCodeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetDeckOwner(userId, guestId, out var owner, out var error))
        {
            return BadRequest(error);
        }

        var result = await _deckService.ImportCodeAsync(owner, request, cancellationToken);
        if (!result.Success || result.Data == null)
        {
            return IsNotFound(result.Error) ? NotFound(result.Error) : BadRequest(result.Error);
        }

        return Ok(result.Data);
    }

    private static bool TryGetDeckOwner(Guid? userId, Guid? guestId, out DeckOwner owner, out string? error)
    {
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            owner = new DeckOwner(userId, null);
            error = null;
            return true;
        }

        if (guestId.HasValue && guestId.Value != Guid.Empty)
        {
            owner = new DeckOwner(null, guestId);
            error = null;
            return true;
        }

        owner = default;
        error = "userId or guestId is required.";
        return false;
    }

    private static bool IsNotFound(string? error)
    {
        return !string.IsNullOrWhiteSpace(error) &&
               error.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }
}
