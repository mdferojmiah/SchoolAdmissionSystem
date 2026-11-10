using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Exceptions;

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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClassResponse>>> List(
        CancellationToken cancellationToken)
    {
        var classes = await classService.ListAsync(cancellationToken);
        return Ok(classes.Select(ToResponse).ToList());
    }

    [HttpGet("{classId}")]
    public async Task<ActionResult<ClassResponse>> Get(
        string classId,
        CancellationToken cancellationToken)
    {
        var schoolClass = await classService.GetAsync(classId, cancellationToken);

        if (schoolClass is null)
        {
            throw new AppException(404, "CLASS_NOT_FOUND", "Class not found.");
        }

        return Ok(ToResponse(schoolClass));
    }

    private static ClassResponse ToResponse(ClassSummary summary) =>
        new(
            summary.Id,
            summary.Name,
            summary.TotalSeats,
            summary.HeldSeats,
            summary.ConfirmedSeats,
            summary.AvailableSeats);
}
