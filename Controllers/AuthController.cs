
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

using System;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;

using WebApplication1.Models;

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
        // JWT CONFIGURATION HELPERS
        // =====================================

        private string GetJwtKey()
        {
            string key = _config["Jwt:Key"] ?? "";

            if (string.IsNullOrWhiteSpace(key) ||
                Encoding.UTF8.GetByteCount(key) < 32)
            {
                throw new InvalidOperationException(
                    "JWT Key must contain at least 32 bytes.");
            }

            return key;
        }

        private string GetJwtIssuer()
        {
            return string.IsNullOrWhiteSpace(_config["Jwt:Issuer"])
                ? "AirborneAviationApi"
                : _config["Jwt:Issuer"]!;
        }

        private string GetJwtAudience()
        {
            return string.IsNullOrWhiteSpace(_config["Jwt:Audience"])
                ? "AirborneAviationClient"
                : _config["Jwt:Audience"]!;
        }

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

            // JWT configuration
            string key = GetJwtKey();
            string issuer = GetJwtIssuer();
            string audience = GetJwtAudience();

            int minutes = int.TryParse(
                _config["Jwt:ExpiryMinutes"],
                out int configuredMinutes) &&
                configuredMinutes > 0
                    ? configuredMinutes
                    : 60;

            DateTime nowUtc = DateTime.UtcNow;
            DateTime expiresUtc = nowUtc.AddMinutes(minutes);

            // Unique token ID
            string tokenId = Guid.NewGuid().ToString();

            // Claims must match Program.cs
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
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key));

            var credentials =
                new SigningCredentials(
                    signingKey,
                    SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: nowUtc,
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

            if (userId <= 0 ||
                string.IsNullOrWhiteSpace(loginId))
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

            object? result =
                await update.ExecuteScalarAsync();

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
        // 4. UPDATE PROFILE NAME
        // =====================================

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please enter a valid name."
                });
            }

            int userId = GetUserId();

            if (userId <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid or expired login token. Please login again."
                });
            }

            string fullName = request.FullName.Trim();

            if (fullName.Length < 2 || fullName.Length > 150)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Name must be between 2 and 150 characters."
                });
            }

            try
            {
                await using var con =
                    new SqlConnection(ConnectionString);

                await using var cmd =
                    new SqlCommand(
                        "dbo.SP_UpdateUserProfile", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(
                    "@UserId", SqlDbType.Int)
                    .Value = userId;

                cmd.Parameters.Add(
                    "@FullName", SqlDbType.NVarChar, 150)
                    .Value = fullName;

                await con.OpenAsync();

                object? result =
                    await cmd.ExecuteScalarAsync();

                int updatedRows =
                    result == null || result == DBNull.Value
                        ? 0
                        : Convert.ToInt32(result);

                if (updatedRows != 1)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "User not found or inactive."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Profile updated successfully.",
                    userId,
                    fullName
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Profile update failed. Please try again."
                });
            }
        }

        // =====================================
        // 5. LOGOUT
        // =====================================

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? jti =
                User.FindFirst(
                    JwtRegisteredClaimNames.Jti)?.Value;

            string? exp =
                User.FindFirst(
                    JwtRegisteredClaimNames.Exp)?.Value;

            if (!Guid.TryParse(jti, out Guid tokenId) ||
                !long.TryParse(exp, out long expiry))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token claims are invalid."
                });
            }

            DateTime expiryUtc;

            try
            {
                expiryUtc =
                    DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid token expiry."
                });
            }

            await using var con =
                new SqlConnection(ConnectionString);

            await using var cmd =
                new SqlCommand(
                    @"IF NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.RevokedTokens
                        WHERE TokenId = @TokenId
                    )
                    BEGIN
                        INSERT INTO dbo.RevokedTokens
                        (
                            TokenId,
                            ExpiryUtc,
                            RevokedAtUtc
                        )
                        VALUES
                        (
                            @TokenId,
                            @ExpiryUtc,
                            SYSUTCDATETIME()
                        );
                    END;",
                    con);

            cmd.Parameters.Add(
                "@TokenId",
                SqlDbType.UniqueIdentifier)
                .Value = tokenId;

            cmd.Parameters.Add(
                "@ExpiryUtc",
                SqlDbType.DateTime2)
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
        // 6. DELETE ACCOUNT
        // =====================================

        [Authorize]
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount(
            [FromBody] DeleteAccountRequest request)
        {
            int userId = GetUserId();
            string loginId = GetLoginId();

            if (userId <= 0 ||
                string.IsNullOrWhiteSpace(loginId))
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

            int affected =
                result == null || result == DBNull.Value
                    ? 0
                    : Convert.ToInt32(result);

            if (affected == 1)
            {
                return Ok(new
                {
                    success = true,
                    message = "Account deleted successfully."
                });
            }

            return NotFound(new
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
