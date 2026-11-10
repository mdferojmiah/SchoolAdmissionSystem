using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Classes;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public sealed class ClassesController(ClassService classService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateClassResponse>> Create(
        [FromBody] CreateClassRequest request,
        CancellationToken cancellationToken)
    {
        var created = await classService.CreateAsync(
            request.Name,
            request.TotalSeats,
            cancellationToken);

        return Created(
            $"/classes/{created.Id}",
            new CreateClassResponse(created.Id, created.Name, created.TotalSeats));
    }
}
