
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// CONTROLLERS
// =====================================

builder.Services.AddControllers();

// =====================================
// JWT CONFIGURATION
// =====================================

var configuredJwtSecret =
    builder.Configuration["Jwt:Secret"] ?? string.Empty;

var jwtSecret = Encoding.UTF8.GetByteCount(configuredJwtSecret) >= 32
    ? configuredJwtSecret
    : "CONFIGURE_Jwt__Secret_WITH_A_RANDOM_SECRET_OF_32_BYTES_MINIMUM";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"]
                ?? "AirborneAviationApi",

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"]
                ?? "AirborneAviationClient",

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                var connectionString = context.HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(jti) ||
                    string.IsNullOrWhiteSpace(connectionString))
                {
                    context.Fail(
                        "Token validation could not be completed.");
                    return;
                }

                try
                {
                    await using var connection =
                        new SqlConnection(connectionString);

                    await using var command = new SqlCommand(
                        @"SELECT CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM dbo.RevokedAuthTokens
                                WHERE Jti = @Jti
                            )
                            OR NOT EXISTS (
                                SELECT 1
                                FROM dbo.AuthUsers
                                WHERE UserId = @UserId
                            )
                            THEN 1 ELSE 0 END;",
                        connection);

                    command.Parameters.AddWithValue("@Jti", jti);

                    command.Parameters.AddWithValue(
                        "@UserId",
                        int.TryParse(
                            context.Principal?
                                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                            out var id) ? id : -1);

                    await connection.OpenAsync(
                        context.HttpContext.RequestAborted);

                    var invalid = Convert.ToInt32(
                        await command.ExecuteScalarAsync(
                            context.HttpContext.RequestAborted));

                    if (invalid == 1)
                    {
                        context.Fail(
                            "Token has been revoked or the account no longer exists.");
                    }
                }
                catch (SqlException)
                {
                    context.Fail(
                        "Token validation service is unavailable.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

// =====================================
// SWAGGER
// =====================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =====================================
// CORS CONFIGURATION
// =====================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AirborneFrontend", policy =>
    {
        policy
            .WithOrigins(
                // Current Netlify frontend
                "https://airborne-frontend.netlify.app",

                // Other Netlify websites
                "https://marvelous-bienenstitch-ef5ebc.netlify.app",
                "https://airborne-aviation-web.netlify.app",

                // Local frontend development
                "http://localhost:5500",
                "http://127.0.0.1:5500",

                "http://localhost:5501",
                "http://127.0.0.1:5501",

                "http://localhost:5502",
                "http://127.0.0.1:5502"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =====================================
// BUILD APPLICATION
// =====================================

var app = builder.Build();

// =====================================
// MIDDLEWARE
// =====================================

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// CORS before Authentication and Authorization
app.UseCors("AirborneFrontend");

app.UseAuthentication();
app.UseAuthorization();

// =====================================
// API CONTROLLERS
// =====================================

app.MapControllers();

// =====================================
// ROOT API
// =====================================

app.MapGet("/", () => Results.Ok(new
{
    success = true,
    message = "Airborne Backend is Running Successfully!",
    status = "Live",
    documentation = "/swagger/index.html"
}));

app.Run();
