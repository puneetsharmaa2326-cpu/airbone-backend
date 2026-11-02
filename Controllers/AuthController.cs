
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using BCrypt.Net;

namespace Practice_Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
        {
            _config = config;
        }

        private string ConnectionString =>
            _config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string missing.");

        // 1. SIGN UP
        [HttpPost("signup")]
        public async Task<IActionResult> Signup(
            [FromBody] SignupRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            string hash = BCrypt.Net.BCrypt.HashPassword(
                request.Password);

            await using var con = new SqlConnection(ConnectionString);
            await using var cmd = new SqlCommand("dbo.CreateUser", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("@LoginId", SqlDbType.NVarChar, 100)
                .Value = request.LoginId.Trim();

            cmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255)
                .Value = hash;

            cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150)
                .Value = request.FullName.Trim();

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            int success = Convert.ToInt32(reader["Success"]);
            string message = Convert.ToString(reader["Message"])!;

            if (success == 0)
                return Conflict(new { success = false, message });

            return Ok(new { success = true, message });
        }

        // 2. LOGIN
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await using var con = new SqlConnection(ConnectionString);
            await using var cmd =
                new SqlCommand("dbo.GetUserForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@LoginId", SqlDbType.NVarChar, 100)
                .Value = request.LoginId.Trim();

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid Login ID or Password"
                });

            int userId = Convert.ToInt32(reader["UserId"]);
            string loginId = Convert.ToString(reader["LoginId"])!;
            string fullName = Convert.ToString(reader["FullName"])!;
            string hash = Convert.ToString(reader["PasswordHash"])!;
            bool isActive = Convert.ToBoolean(reader["IsActive"]);

            if (!isActive ||
                !BCrypt.Net.BCrypt.Verify(request.Password, hash))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid Login ID or Password"
                });
            }

            string key = _config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key missing");

            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException(
                    "JWT key must be at least 32 bytes.");

            int minutes = int.TryParse(
                _config["Jwt:ExpiryMinutes"], out int m) ? m : 60;

            DateTime expires = DateTime.UtcNow.AddMinutes(minutes);
            string tokenId = Guid.NewGuid().ToString();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, loginId),
                new Claim("jti", tokenId)
            };

            var signingKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(
                signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: credentials);

            string tokenString =
                new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                success = true,
                message = "Login successful",
                token = tokenString,
                expiresUtc = expires,
                user = new { userId, loginId, fullName }
            });
        }

        // 3. UPDATE PASSWORD (login/token required)
        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            int userId = GetUserId();
            if (userId <= 0)
                return Unauthorized();

            await using var con = new SqlConnection(ConnectionString);
            await using var cmd = new SqlCommand(
                "dbo.GetUserForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@LoginId", SqlDbType.NVarChar, 100)
                .Value = User.FindFirst(ClaimTypes.Name)?.Value ?? "";

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized();

            string oldHash = Convert.ToString(reader["PasswordHash"])!;
            if (!BCrypt.Net.BCrypt.Verify(
                request.CurrentPassword, oldHash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Current password is incorrect"
                });
            }
            await reader.DisposeAsync();

            string newHash = BCrypt.Net.BCrypt.HashPassword(
                request.NewPassword);

            await using var update = new SqlCommand(
                "dbo.UpdateUserPassword", con);
            update.CommandType = CommandType.StoredProcedure;
            update.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            update.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255)
                .Value = newHash;

            int affected = Convert.ToInt32(
                await update.ExecuteScalarAsync());

            return affected == 1
                ? Ok(new
                {
                    success = true,
                    message = "Password updated. Please log in again."
                })
                : NotFound(new
                {
                    success = false,
                    message = "Active user not found"
                });
        }

        // 4. LOGOUT (current token revoke)
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? jti = User.FindFirst("jti")?.Value;
            string? exp = User.FindFirst("exp")?.Value;

            if (!Guid.TryParse(jti, out Guid tokenId) ||
                !long.TryParse(exp, out long expiry))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token claims are invalid"
                });
            }

            DateTime expiryUtc =
                DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime;

            await using var con = new SqlConnection(ConnectionString);
            await using var cmd = new SqlCommand("dbo.RevokeToken", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@TokenId", SqlDbType.UniqueIdentifier)
                .Value = tokenId;
            cmd.Parameters.Add("@ExpiryUtc", SqlDbType.DateTime2)
                .Value = expiryUtc;

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Logout successful"
            });
        }

        // 5. DELETE ACCOUNT (password required)
        [Authorize]
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount(
            [FromBody] DeleteAccountRequest request)
        {
            int userId = GetUserId();
            if (userId <= 0)
                return Unauthorized();

            await using var con = new SqlConnection(ConnectionString);
            await using var cmd = new SqlCommand(
                "dbo.GetUserForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@LoginId", SqlDbType.NVarChar, 100)
                .Value = User.FindFirst(ClaimTypes.Name)?.Value ?? "";

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized();

            string hash = Convert.ToString(reader["PasswordHash"])!;
            if (!BCrypt.Net.BCrypt.Verify(request.Password, hash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Password is incorrect"
                });
            }
            await reader.DisposeAsync();

            await using var delete = new SqlCommand(
                "dbo.DeleteUserAccount", con);
            delete.CommandType = CommandType.StoredProcedure;
            delete.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

            int affected = Convert.ToInt32(
                await delete.ExecuteScalarAsync());

            return affected == 1
                ? Ok(new
                {
                    success = true,
                    message = "Account deleted successfully"
                })
                : NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
        }

        private int GetUserId()
        {
            return int.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out int id) ? id : 0;
        }
    }

    public class SignupRequest
    {
        [Required, StringLength(100, MinimumLength = 3)]
        public string LoginId { get; set; } = "";

        [Required, StringLength(150, MinimumLength = 2)]
        public string FullName { get; set; } = "";

        [Required, MinLength(8)]
        public string Password { get; set; } = "";
    }

    public class LoginRequest
    {
        [Required]
        public string LoginId { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = "";

        [Required, MinLength(8)]
        public string NewPassword { get; set; } = "";
    }

    public class DeleteAccountRequest
    {
        [Required]
        public string Password { get; set; } = "";
    }
}
