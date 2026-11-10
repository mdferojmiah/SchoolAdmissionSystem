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

    [HttpPost("{holdId}/confirm")]
    public async Task<ActionResult<ConfirmHoldResponse>> Confirm(
        string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var validatedStudentId = StudentIdValidator.Validate(studentId);
        var confirmation = await holdService.ConfirmAsync(
            holdId,
            validatedStudentId,
            cancellationToken);

        return Ok(new ConfirmHoldResponse(
            confirmation.HoldId,
            confirmation.Status));
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> Cancel(
        string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var validatedStudentId = StudentIdValidator.Validate(studentId);
        await holdService.CancelAsync(holdId, validatedStudentId, cancellationToken);

        return NoContent();
    }
}
