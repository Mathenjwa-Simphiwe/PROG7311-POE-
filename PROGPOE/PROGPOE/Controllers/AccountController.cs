using Microsoft.AspNetCore.Mvc;
using PROGPOE.Services;

namespace PROGPOE.Controllers
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MVC Account Controller
    //  Handles the login page and delegates authentication to TechMoveAPI.
    //  On success, stores the JWT in the session so TechMoveApiService can
    //  attach it to outbound API calls automatically.
    // ─────────────────────────────────────────────────────────────────────────
    public class AccountController : Controller
    {
        private readonly TechMoveApiService _api;

        public AccountController(TechMoveApiService api) => _api = api;

        // GET /Account/Login
        [HttpGet]
        [Route("Account/Login")]
        public IActionResult Login() => View();

        // POST /Account/Login
        [HttpPost]
        [Route("Account/Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            var result = await _api.LoginAsync(email, password);

            if (result == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View();
            }

            // Store JWT and user info in the session
            HttpContext.Session.SetString("JwtToken", result.Value.GetProperty("token").GetString()!);
            HttpContext.Session.SetString("UserRole",  result.Value.GetProperty("role").GetString()!);
            HttpContext.Session.SetString("FullName",  result.Value.GetProperty("fullName").GetString()!);

            var role = result.Value.GetProperty("role").GetString();

            return role == "Admin"
                ? RedirectToAction("Dashboard", "Admin")
                : RedirectToAction("Dashboard", "Client");
        }

        // GET /Account/Logout
        [HttpGet]
        [Route("Account/Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
