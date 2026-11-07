using EventPlatform.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Controllers.Api;

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
[Tags("Events")]
public class CategoriesApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesApiController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Lists the category values used by events.</summary>
    [HttpGet]
    [EndpointSummary("List categories")]
    [EndpointDescription("Category values accepted by the category filter of GET /api/v1/events (the site shows them translated to Turkish).")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> List()
    {
        var names = await _context.Interests
            .AsNoTracking()
            .OrderBy(i => i.Interestname)
            .Select(i => i.Interestname)
            .ToListAsync();

        return Ok(names);
    }
}
