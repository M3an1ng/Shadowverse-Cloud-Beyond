using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Domain.Entities;
namespace ShadowverseBeyond.Server.Controllers;
[ApiController, Route("api/card-styles")]
public sealed class CardStylesController(ApplicationDbContext db, ShadowverseBeyond.Server.Services.IGameService games, IWebHostEnvironment environment) : ControllerBase
{
    private async Task<User?> Actor(CancellationToken ct) { if (!Guid.TryParse(Request.Headers["X-Editor-UserId"].FirstOrDefault(), out var id)) return null; return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive, ct); }
    [HttpPost("art"), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null || UserRolePermissions.GetPermissionLevel(actor.Role) < 10) return StatusCode(403);
        if (file == null || file.Length == 0 || file.Length > 10 * 1024 * 1024) return BadRequest(new { error = "图片大小应在10MB以内" });
        var bytes = new byte[12]; await using (var stream = file.OpenReadStream()) { if (await stream.ReadAsync(bytes, ct) < 12) return BadRequest(); }
        string? extension = bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 ? ".png" : bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? ".jpg" : System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? ".webp" : null;
        if (extension == null) return BadRequest(new { error = "只支持PNG、JPEG、WebP图片" }); var name = Guid.NewGuid().ToString("N") + extension; var folder = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "assets", "card-styles")); Directory.CreateDirectory(folder); await using (var output = System.IO.File.Create(Path.Combine(folder, name))) await file.CopyToAsync(output, ct); return Ok(new { url = $"/assets/card-styles/{name}" });
    }
    [HttpGet] public IActionResult List() => Ok(CardStyleCatalog.All);
    [HttpGet("{cardId:int}")] public IActionResult Get(int cardId) => Ok(CardStyleCatalog.Get(cardId));
    private static bool SafeImage(string? url) => string.IsNullOrWhiteSpace(url) || url.Length <= 2000 && (url.StartsWith("/assets/") && !url.Contains("..") && !url.Contains('\\') || Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
    private static bool Valid(CardAppearance a) => a != null && !string.IsNullOrWhiteSpace(a.Name) && a.Name.Length <= 100 && a.Description.Length <= 4000 && SafeImage(a.ArtPreview) && SafeImage(a.ArtDetail);
    [HttpPut("{cardId:int}")]
    public async Task<IActionResult> Save(int cardId, StyleConfiguration config, CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null || UserRolePermissions.GetPermissionLevel(actor.Role) < 10) return StatusCode(403);
        if (cardId != config.CardId || config.Styles == null || config.Styles.Count > 30 || config.Styles.Any(s => s == null || !Regex.IsMatch(s.Id ?? "", "^[a-zA-Z0-9_-]{1,100}$") || string.IsNullOrWhiteSpace(s.Label) || s.Label.Length > 60 || !Valid(s.Appearance) || s.Bindings == null || s.Bindings.Count > 30 || s.Bindings.Any(b => b == null || !Valid(b.Appearance))) || config.Styles.Select(s => s.Id).Distinct().Count() != config.Styles.Count) return BadRequest(new { error = "卡片风格格式不正确" });
        if (!await db.CardDefinitions.AnyAsync(c => c.Id == cardId, ct)) return NotFound();
        foreach (var style in config.Styles) { if (style.Bindings.Select(b => b.CardId).Distinct().Count() != style.Bindings.Count) return BadRequest(new { error = "绑定卡牌重复" }); foreach (var binding in style.Bindings) { var parent = CardStyleCatalog.Parent(binding.CardId); if (binding.CardId == cardId || parent != null && parent != cardId || CardStyleCatalog.Parent(cardId) != null || CardStyleCatalog.Get(binding.CardId).Styles.Any(s => s.Bindings.Count > 0) || !await db.CardDefinitions.AnyAsync(c => c.Id == binding.CardId, ct)) return BadRequest(new { error = "绑定卡不存在、已绑定其他主卡或形成循环" }); } }
        var key = $"style:{cardId}"; var doc = await db.FeatureDocuments.FindAsync(new object[] { key }, ct); if ((doc?.Revision ?? 0) != config.Revision) return Conflict(new { error = "风格已被其他窗口修改，请重新打开" });
        if (doc == null) { doc = new() { Id = key }; db.Add(doc); }
        doc.Revision++; doc.UpdatedAt = DateTime.UtcNow; doc.Json = JsonSerializer.Serialize(config with { Revision = doc.Revision }, CardStyleCatalog.JsonOptions); await db.SaveChangesAsync(ct); CardStyleCatalog.Apply(doc); return Ok(CardStyleCatalog.Get(cardId));
    }
    [HttpGet("preferences")] public async Task<IActionResult> Preferences(CancellationToken ct) { var actor = await Actor(ct); return actor == null ? StatusCode(403) : Ok(CardStyleCatalog.Preferences(actor.Id)); }
    public sealed record StyleChoice(int CardId, string? StyleId);
    [HttpPut("preferences")]
    public async Task<IActionResult> Choose(StyleChoice choice, CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403); if (CardStyleCatalog.Parent(choice.CardId) != null) return BadRequest(new { error = "请在绑定的主卡中选择风格" });
        if (choice.StyleId != null && !CardStyleCatalog.Get(choice.CardId).Styles.Any(s => s.Id == choice.StyleId)) return BadRequest(new { error = "卡片风格不存在" });
        var prefs = CardStyleCatalog.Preferences(actor.Id); if (choice.StyleId == null) prefs.Remove(choice.CardId); else prefs[choice.CardId] = choice.StyleId;
        var key = $"style-prefs:{actor.Id}"; var doc = await db.FeatureDocuments.FindAsync(new object[] { key }, ct); if (doc == null) { doc = new() { Id = key }; db.Add(doc); }
        doc.Json = JsonSerializer.Serialize(prefs, CardStyleCatalog.JsonOptions); doc.Revision++; doc.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); CardStyleCatalog.Apply(doc); await games.NotifyCardStylesChangedAsync(actor.Id); return Ok(prefs);
    }
}
