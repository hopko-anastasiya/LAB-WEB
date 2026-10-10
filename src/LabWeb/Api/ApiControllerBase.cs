using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace LabWeb.Api;

[ApiController]
[OutputCache(PolicyName = ApiCache.Data)]
public abstract class ApiControllerBase : ControllerBase
{
    protected string? AbsoluteUrl(string? path) =>
        string.IsNullOrEmpty(path) ? null : $"{Request.Scheme}://{Request.Host}{Request.PathBase}{path}";

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Не знайдено", detail: detail);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Конфлікт", detail: detail);

    protected ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
