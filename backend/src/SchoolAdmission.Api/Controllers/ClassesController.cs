using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Holds;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public sealed class ClassesController(
    ClassService classService,
    HoldService holdService) : ControllerBase
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

    [HttpPost("{classId}/holds")]
    public async Task<ActionResult<ApplyHoldResponse>> Apply(
        string classId,
        [FromHeader(Name = "X-Student-Id")] string? studentId,
        CancellationToken cancellationToken)
    {
        var validatedStudentId = StudentIdValidator.Validate(studentId);
        var hold = await holdService.ApplyAsync(
            classId,
            validatedStudentId,
            cancellationToken);

        return Created(
            $"/holds/{hold.HoldId}",
            new ApplyHoldResponse(
                hold.HoldId,
                hold.ClassId,
                hold.StudentId,
                hold.Status,
                hold.ExpiresAt));
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
