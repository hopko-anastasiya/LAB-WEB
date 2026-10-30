using LabWeb.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace LabWeb.Api;

[ApiController]
[OutputCache(PolicyName = ApiCache.Data)]
public abstract class ApiControllerBase : ControllerBase
{
    protected string? AbsoluteUrl(string? path) =>
        string.IsNullOrEmpty(path) ? null
        : path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path
        : $"{Request.Scheme}://{Request.Host}{Request.PathBase}{path}";

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Не знайдено", detail: detail);

    protected CurrentUser CurrentUser => HttpContext.RequestServices.GetRequiredService<CurrentUser>();

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status403Forbidden, title: "Доступ заборонено", detail: detail);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Конфлікт", detail: detail);

    protected ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
