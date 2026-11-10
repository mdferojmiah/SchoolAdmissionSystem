using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Holds;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public sealed class HoldsController(HoldService holdService) : ControllerBase
{
    [HttpGet("{holdId}")]
    public async Task<ActionResult<HoldStatusResponse>> Get(
        string holdId,
        CancellationToken cancellationToken)
    {
        var hold = await holdService.GetAsync(holdId, cancellationToken);

        return Ok(new HoldStatusResponse(
            hold.HoldId,
            hold.ClassId,
            hold.Status,
            hold.ExpiresAt));
    }
}
