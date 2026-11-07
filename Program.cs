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

// Login aur token validation dono mein Jwt:Key hi use hoga.
var jwtSecret = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtSecret) ||
    Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key appsettings.json mein configure karein. Key minimum 32 bytes ki honi chahiye.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? "AirborneAviationApi";

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? "AirborneAviationClient";

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection appsettings.json mein configure nahi hai.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWT claims ko original names (sub, jti, unique_name) mein rakhega.
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

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
                var principal = context.Principal;

                string? jti = principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                string? userId = principal?
                    .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrWhiteSpace(jti) ||
                    !Guid.TryParse(jti, out _) ||
                    !int.TryParse(userId, out int parsedUserId) ||
                    parsedUserId <= 0)
                {
                    context.Fail("JWT token claims are invalid.");
                    return;
                }

                try
                {
                    await using var connection =
                        new SqlConnection(connectionString);

                    await using var command = new SqlCommand(
                        @"SELECT CASE
                            WHEN EXISTS
                            (
                                SELECT 1
                                FROM dbo.RevokedAuthTokens
                                WHERE Jti = @Jti
                            )
                            THEN 1
                            ELSE 0
                          END;",
                        connection);

                    command.Parameters.Add(
                        "@Jti",
                        System.Data.SqlDbType.UniqueIdentifier
                    ).Value = Guid.Parse(jti);

                    await connection.OpenAsync(
                        context.HttpContext.RequestAborted);

                    var result = await command.ExecuteScalarAsync(
                        context.HttpContext.RequestAborted);

                    bool isRevoked =
                        Convert.ToInt32(result) == 1;

                    if (isRevoked)
                    {
                        context.Fail("This token has been revoked.");
                    }
                }
                catch (SqlException)
                {
                    // Database validation unavailable means token is rejected.
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

builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName);
});

// =====================================
// CORS
// =====================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AirborneFrontend", policy =>
    {
        policy
            .WithOrigins(
                "https://airborne-frontend.netlify.app",
                "https://marvelous-bienenstitch-ef5ebc.netlify.app",
                "https://airborne-aviation-web.netlify.app",

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