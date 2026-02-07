using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/cardsets")]
public class CardSetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CardSetsController> _logger;

    public CardSetsController(ApplicationDbContext db, ILogger<CardSetsController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CardSetDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CardSetDefinitionDto>>> GetCardSets()
    {
        try
        {
            var definitions = await _db.CardSetDefinitions
                .AsNoTracking()
                .OrderBy(set => set.Region)
                .ThenBy(set => set.Sequence)
                .ToListAsync();

            var dtos = definitions.Select(CardSetDefinitionDto.FromEntity).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load card set definitions.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to load card set definitions.");
        }
    }
}
