using Microsoft.AspNetCore.Mvc;

namespace LabWeb.Controllers;

public class GameController : Controller
{
    public IActionResult Index() => View();
}
