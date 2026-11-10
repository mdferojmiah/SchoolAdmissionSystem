using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Holds;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me/holds")]
public sealed class MeHoldsController(HoldService holdService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MyHoldResponse>>> List(
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var validatedStudentId = StudentIdValidator.Validate(studentId);
        var holds = await holdService.ListMineAsync(validatedStudentId, cancellationToken);

        return Ok(holds
            .Select(hold => new MyHoldResponse(
                hold.HoldId,
                hold.ClassId,
                hold.ClassName,
                hold.Status,
                hold.ExpiresAt))
            .ToList());
    }
}
