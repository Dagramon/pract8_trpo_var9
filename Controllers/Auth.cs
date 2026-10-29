using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using pract8_trpo.Models;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace pract8_trpo.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class Auth : ControllerBase
    {
        KaramovDbContext db = new();
        private List<User> _users;
        private readonly IPasswordHasher<User> _hasher;
        private readonly IConfiguration _configuration;
        public Auth(
            List<User> users,
            IPasswordHasher<User> hasher,
            IConfiguration configuration
        )
        {
            _users = db.Users.ToList();
            _hasher = hasher;
            _configuration = configuration;
        }
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, "User")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var credentials = new SigningCredentials(key,
                SecurityAlgorithms.HmacSha256
                );
            var token = new JwtSecurityToken(
                issuer: "pract8_trpo",
                audience: "pract8_client",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public record AuthRequest(string Login, string Password);

        [HttpPost("register")]
        public IActionResult Register(AuthRequest request)
        {
            if (_users.Any(u => u.Login == request.Login))
                return Conflict("Логин уже существует");

            var user = new User
            {
                Login = request.Login,
                Role = "User"
            };

            user.PasswordHash = _hasher.HashPassword(user, request.Password);

            _users.Add(user);

            db.SaveChanges();

            return Ok("Пользователь зарегистрирован");
        }
        [HttpPost("login")]
        public IActionResult Login(AuthRequest request)
        {
            var user = _users.Find(u => u.Login == request.Login);
            if (user == null)
                return Unauthorized("Неверный логин или пароль");

            var result = _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password
            );

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Неверный логин или пароль");

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                access_token = token
            });
        }
    }
}
