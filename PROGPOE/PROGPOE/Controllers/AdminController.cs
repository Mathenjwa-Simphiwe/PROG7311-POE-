using Microsoft.AspNetCore.Mvc;

namespace PROGPOE.Controllers
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MVC CONTROLLER – serves Razor views for the Admin UI.
    //  All data is fetched from TechMoveAPI via JavaScript (fetch/XHR) in the
    //  views, using the JWT stored in sessionStorage after login.
    //
    //  If you need server-side data (e.g. for pre-rendering), inject
    //  TechMoveApiService here and call it before returning the view.
    // ─────────────────────────────────────────────────────────────────────────
    public class AdminController : Controller
    {
        public IActionResult Dashboard()       => View();
        public IActionResult Contracts()       => View();
        public IActionResult ServiceRequests() => View();
    }
}
