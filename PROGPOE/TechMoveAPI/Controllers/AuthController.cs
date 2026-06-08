using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TechMoveAPI.Models;

namespace TechMoveAPI.Controllers
{
    /// <summary>
    /// Handles authentication. Returns a JWT for use in the Authorization header.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<Client> _clientManager;
        private readonly IConfiguration      _config;

        public AuthController(
            UserManager<Client> clientManager,
            IConfiguration      config)
        {
            _clientManager = clientManager;
            _config        = config;
        }

        /// <summary>
        /// Log in and receive a JWT Bearer token.
        /// </summary>
        /// <remarks>
        /// Default admin: admin@glms.com / Admin@123 <br/>
        /// Default client: client@test.com / Client@123
        /// </remarks>
        [HttpPost("login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var client = await _clientManager.FindByEmailAsync(dto.Email);
            if (client != null && await _clientManager.CheckPasswordAsync(client, dto.Password))
            {
                var roles = await _clientManager.GetRolesAsync(client);
                var role = roles.Contains("Admin") ? "Admin" : "Client";

                return Ok(new
                {
                    Token    = BuildToken(client.Id, role),
                    Role     = role,
                    FullName = client.FullName,
                    UserId   = client.Id
                });
            }

            return Unauthorized(new { Message = "Invalid email or password." });
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private string BuildToken(string userId, string role)
        {
            var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer:             _config["Jwt:Issuer"],
                audience:           _config["Jwt:Audience"],
                claims:             claims,
                expires:            DateTime.Now.AddHours(3),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // ── DTO ─────────────────────────────────────────────────────────────────
    public class LoginDto
    {
        public string Email    { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
