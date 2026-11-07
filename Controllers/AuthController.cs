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

        // =====================================
        // COMMON JWT CLAIM HELPERS
        // =====================================

        private int GetUserId()
        {
            return int.TryParse(
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out int id) ? id : 0;
        }

        private string GetLoginId()
        {
            return User.FindFirst(
                JwtRegisteredClaimNames.UniqueName)?.Value ?? "";
        }

        // =====================================
        // 1. SIGNUP
        // =====================================

        [HttpPost("signup")]
        public async Task<IActionResult> Signup(
            [FromBody] SignupRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            string hash = BCrypt.Net.BCrypt.HashPassword(
                request.Password);

            await using var con =
                new SqlConnection(ConnectionString);

            await using var cmd =
                new SqlCommand("dbo.CreateUser", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(
                "@LoginId", SqlDbType.NVarChar, 100)
                .Value = request.LoginId.Trim();

            cmd.Parameters.Add(
                "@PasswordHash", SqlDbType.NVarChar, 255)
                .Value = hash;

            cmd.Parameters.Add(
                "@FullName", SqlDbType.NVarChar, 150)
                .Value = request.FullName.Trim();

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Signup response was not returned by database."
                });
            }

            int success = Convert.ToInt32(reader["Success"]);
            string message =
                Convert.ToString(reader["Message"]) ?? "";

            if (success == 0)
            {
                return Conflict(new
                {
                    success = false,
                    message
                });
            }

            return Ok(new
            {
                success = true,
                message
            });
        }

        // =====================================
        // 2. LOGIN
        // =====================================

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await using var con =
                new SqlConnection(ConnectionString);

            await using var cmd =
                new SqlCommand("dbo.GetUserForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(
                "@LoginId", SqlDbType.NVarChar, 100)
                .Value = request.LoginId.Trim();

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid Login ID or Password"
                });
            }

            int userId = Convert.ToInt32(reader["UserId"]);

            string loginId =
                Convert.ToString(reader["LoginId"]) ?? "";

            string fullName =
                Convert.ToString(reader["FullName"]) ?? "";

            string hash =
                Convert.ToString(reader["PasswordHash"]) ?? "";

            bool isActive =
                Convert.ToBoolean(reader["IsActive"]);

            if (!isActive ||
                !BCrypt.Net.BCrypt.Verify(request.Password, hash))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid Login ID or Password"
                });
            }

            // Same key used by Program.cs
            string key = _config["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "Jwt:Key is missing.");

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT key must be at least 32 bytes.");
            }

            string issuer = _config["Jwt:Issuer"]
                ?? "AirborneAviationApi";

            string audience = _config["Jwt:Audience"]
                ?? "AirborneAviationClient";

            int minutes = int.TryParse(
                _config["Jwt:ExpiryMinutes"],
                out int m) ? m : 60;

            DateTime expiresUtc =
                DateTime.UtcNow.AddMinutes(minutes);

            string tokenId = Guid.NewGuid().ToString();

            // IMPORTANT:
            // Claims must match Program.cs validation.
            var claims = new[]
            {
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    userId.ToString()),

                new Claim(
                    JwtRegisteredClaimNames.UniqueName,
                    loginId),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    tokenId)
            };

            var signingKey =
                new SymmetricSecurityKey(keyBytes);

            var credentials =
                new SigningCredentials(
                    signingKey,
                    SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresUtc,
                signingCredentials: credentials);

            string tokenString =
                new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                success = true,
                message = "Login successful",
                token = tokenString,
                expiresUtc,
                user = new
                {
                    userId,
                    loginId,
                    fullName
                }
            });
        }

        // =====================================
        // 3. CHANGE PASSWORD
        // =====================================

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            int userId = GetUserId();
            string loginId = GetLoginId();

            if (userId <= 0 || string.IsNullOrWhiteSpace(loginId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid or expired login token. Please login again."
                });
            }

            await using var con =
                new SqlConnection(ConnectionString);

            await con.OpenAsync();

            // Verify old password
            await using var cmd =
                new SqlCommand("dbo.GetUserForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(
                "@LoginId", SqlDbType.NVarChar, 100)
                .Value = loginId;

            string oldHash;

            await using (var reader =
                await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not found. Please login again."
                    });
                }

                bool isActive =
                    Convert.ToBoolean(reader["IsActive"]);

                if (!isActive)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Your account is inactive."
                    });
                }

                oldHash =
                    Convert.ToString(reader["PasswordHash"]) ?? "";
            }

            if (!BCrypt.Net.BCrypt.Verify(
                request.CurrentPassword, oldHash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Current password is incorrect."
                });
            }

            if (request.CurrentPassword == request.NewPassword)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "New password must be different from old password."
                });
            }

            string newHash =
                BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            await using var update =
                new SqlCommand("dbo.UpdateUserPassword", con);

            update.CommandType = CommandType.StoredProcedure;

            update.Parameters.Add(
                "@UserId", SqlDbType.Int)
                .Value = userId;

            update.Parameters.Add(
                "@PasswordHash", SqlDbType.NVarChar, 255)
                .Value = newHash;

            object? result = await update.ExecuteScalarAsync();

            int affected = result == null ||
                           result == DBNull.Value
                ? 0
                : Convert.ToInt32(result);

            if (affected != 1)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Password could not be updated. User not found."
                });
            }

            return Ok(new
            {
                success = true,
                message = "Password updated successfully. Please login again."
            });
        }

        // =====================================
        // 4. LOGOUT
        // =====================================

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? jti =
                User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            string? exp =
                User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            if (!Guid.TryParse(jti, out Guid tokenId) ||
                !long.TryParse(exp, out long expiry))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token claims are invalid."
                });
            }

            DateTime expiryUtc =
                DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime;

            await using var con =
                new SqlConnection(ConnectionString);

            await using var cmd = new SqlCommand(
                @"IF NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.RevokedAuthTokens
                      WHERE Jti = @Jti
                  )
                  BEGIN
                      INSERT INTO dbo.RevokedAuthTokens
                          (Jti, ExpiryUtc)
                      VALUES
                          (@Jti, @ExpiryUtc);
                  END;",
                con);

            cmd.Parameters.Add(
                "@Jti", SqlDbType.UniqueIdentifier)
                .Value = tokenId;

            cmd.Parameters.Add(
                "@ExpiryUtc", SqlDbType.DateTime2)
                .Value = expiryUtc;

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Logout successful."
            });
        }

        // =====================================
        // 5. DELETE ACCOUNT
        // =====================================

        [Authorize]
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount(
            [FromBody] DeleteAccountRequest request)
        {
            int userId = GetUserId();
            string loginId = GetLoginId();

            if (userId <= 0 || string.IsNullOrWhiteSpace(loginId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid login token."
                });
            }

            await using var con =
                new SqlConnection(ConnectionString);

            await con.OpenAsync();

            await using var cmd =
                new SqlCommand("dbo.GetUserForLogin", con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(
                "@LoginId", SqlDbType.NVarChar, 100)
                .Value = loginId;

            string hash;

            await using (var reader =
                await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not found."
                    });
                }

                hash =
                    Convert.ToString(reader["PasswordHash"]) ?? "";
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, hash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Password is incorrect."
                });
            }

            await using var delete =
                new SqlCommand("dbo.DeleteUserAccount", con);

            delete.CommandType = CommandType.StoredProcedure;

            delete.Parameters.Add(
                "@UserId", SqlDbType.Int)
                .Value = userId;

            object? result =
                await delete.ExecuteScalarAsync();

            int affected = result == null ||
                           result == DBNull.Value
                ? 0
                : Convert.ToInt32(result);

            return affected == 1
                ? Ok(new
                {
                    success = true,
                    message = "Account deleted successfully."
                })
                : NotFound(new
                {
                    success = false,
                    message = "User not found."
                });
        }
    }

    // =====================================
    // REQUEST MODELS
    // =====================================

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