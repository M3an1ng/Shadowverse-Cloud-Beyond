using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AbilityEditorMetadataController : ControllerBase
    {
        private readonly IAbilityEditorMetadataService _metadataService;

        public AbilityEditorMetadataController(IAbilityEditorMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        [HttpGet]
        public ActionResult<AbilityEditorMetadataDto> Get()
        {
            return Ok(_metadataService.GetAbilityEditorMetadata());
        }
    }
}