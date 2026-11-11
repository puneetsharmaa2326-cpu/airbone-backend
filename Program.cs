
using System;
using System.Data;
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
// DATABASE CONFIGURATION
// =====================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection is missing.");
}

// =====================================
// JWT CONFIGURATION
// =====================================

var jwtSecret = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtSecret) ||
    Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "JWT Key must contain at least 32 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
    jwtIssuer = "AirborneAviationApi";

var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtAudience))
    jwtAudience = "AirborneAviationClient";

var jwtKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtSecret));

// =====================================
// AUTHENTICATION
// =====================================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwtKey,

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromSeconds(30),

                RequireExpirationTime = true,
                RequireSignedTokens = true
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
                    !Guid.TryParse(jti, out Guid tokenId) ||
                    !int.TryParse(userId, out int parsedUserId) ||
                    parsedUserId <= 0)
                {
                    context.Fail("Invalid JWT claims.");
                    return;
                }

                try
                {
                    await using var connection =
                        new SqlConnection(connectionString);

                    await using var command =
                        new SqlCommand(
                            @"SELECT CASE
                                WHEN EXISTS
                                (
                                    SELECT 1
                                    FROM dbo.RevokedTokens
                                    WHERE TokenId = @TokenId
                                )
                                THEN 1
                                ELSE 0
                            END;",
                            connection);

                    command.Parameters.Add(
                        "@TokenId",
                        SqlDbType.UniqueIdentifier
                    ).Value = tokenId;

                    await connection.OpenAsync(
                        context.HttpContext.RequestAborted);

                    var result = await command.ExecuteScalarAsync(
                        context.HttpContext.RequestAborted);

                    bool isRevoked =
                        Convert.ToInt32(result) == 1;

                    if (isRevoked)
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
                catch (Exception ex)
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JWTValidation");

                    logger.LogError(
                        ex,
                        "JWT database validation failed.");

                    // Fail closed: database validation failure
                    // must not authorize the request.
                    context.Fail(
                        "Token validation service is unavailable.");
                }
            }
        };
    });

// =====================================
// AUTHORIZATION
// =====================================

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

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AirborneFrontend", policy =>
//    {
//        policy
//            .WithOrigins(
//                "https://airborne-frontend.netlify.app",
//                "https://marvelous-bienenstitch-ef5ebc.netlify.app",
//                "https://airborne-aviation-web.netlify.app",

//                "http://localhost:5500",
//                "http://127.0.0.1:5500",

//                "http://localhost:5501",
//                "http://127.0.0.1:5501",

//                "http://localhost:5502",
//                "http://127.0.0.1:5502"
//            )
//            .AllowAnyHeader()
//            .AllowAnyMethod();
//    });
//});



builder.Services.AddCors(options =>
{
    options.AddPolicy("AirborneFrontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});



// =====================================
// BUILD APPLICATION
// =====================================

//var app = builder.Build();

//// =====================================
//// MIDDLEWARE
//// =====================================

//app.UseSwagger();

//app.UseSwaggerUI();

//app.UseHttpsRedirection();

//app.UseDefaultFiles();

//app.UseStaticFiles();

//app.UseRouting();

//app.UseCors("AirborneFrontend");

//app.UseAuthentication();

//app.UseAuthorization();




var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// CORS - Allow All Origins
app.UseCors("AirborneFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();




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
