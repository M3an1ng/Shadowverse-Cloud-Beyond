using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Dtos.Emotes;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/emotes")]
    public class EmotesController : ControllerBase
    {
        private readonly IEmoteCatalogService _emoteCatalogService;

        public EmotesController(IEmoteCatalogService emoteCatalogService)
        {
            _emoteCatalogService = emoteCatalogService;
        }

        [HttpGet]
        public ActionResult<EmoteCatalogDto> Get()
        {
            return Ok(_emoteCatalogService.GetCatalog());
        }
    }
}
