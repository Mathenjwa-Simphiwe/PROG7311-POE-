using Microsoft.AspNetCore.Mvc;

namespace PROGPOE.Controllers
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MVC CONTROLLER – serves Razor views for the Client UI.
    //  All data is fetched from TechMoveAPI via JavaScript in the views.
    // ─────────────────────────────────────────────────────────────────────────
    public class ClientController : Controller
    {
        public IActionResult Dashboard()       => View();
        public IActionResult ClientContracts() => View();
        public IActionResult Requests()        => View();
    }
}
