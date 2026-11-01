using LabWeb.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabWeb.Controllers;

[Authorize(Policy = Policies.Member)]
public class GuideController : Controller
{
    public IActionResult Index() => View();
}
