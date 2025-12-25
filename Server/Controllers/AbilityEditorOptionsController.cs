using Microsoft.AspNetCore.Mvc;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/abilityEditorOptions")]
    public class AbilityEditorOptionsController : ControllerBase
    {
        [HttpGet("{key}")]
        public ActionResult<Dictionary<string, string>> Get(string key)
        {
            // Placeholder implementation: no option providers are registered yet.
            // Returning an empty map allows the frontend to render a dropdown without hardcoding.
            return Ok(new Dictionary<string, string>());
        }
    }
}

