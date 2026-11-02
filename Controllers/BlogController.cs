
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public BlogController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Database Connection
    private SqlConnection CreateConnection() => new(
        _configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is not configured."));


    // ==========================================
    // ADMIN API KEY VALIDATION
    // ==========================================

    // Check Admin API Key
    private bool AdminKeyIsValid(string? suppliedKey = null)
    {
        var configuredKey = _configuration["BlogAdmin:ApiKey"];

        // Agar method mein key nahi mili to Request Header se lo
        if (string.IsNullOrWhiteSpace(suppliedKey))
        {
            suppliedKey = Request.Headers["X-Admin-Key"].ToString();
        }

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            string.IsNullOrWhiteSpace(suppliedKey))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(configuredKey);
        var actual = Encoding.UTF8.GetBytes(suppliedKey);

        return expected.Length == actual.Length &&
               CryptographicOperations.FixedTimeEquals(
                   expected, actual);
    }


    // Verify Admin API Key
    private IActionResult? RequireAdminKey(string? suppliedKey = null)
    {
        var configuredKey = _configuration["BlogAdmin:ApiKey"];

        // Server par API Key configured hai ya nahi
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(503, new
            {
                message = "Blog admin API key is not configured on the server."
            });
        }

        // Supplied API Key check
        if (!AdminKeyIsValid(suppliedKey))
        {
            return Unauthorized(new
            {
                message = "Missing or invalid X-Admin-Key header."
            });
        }

        return null;
    }


    // ==========================================
    // READ BLOG DATA
    // ==========================================

    // Read Blog From Database
    private static BlogModel ReadBlog(SqlDataReader reader) => new()
    {
        BlogId = Convert.ToInt32(reader["BlogId"]),
        Title = Convert.ToString(reader["Title"]) ?? string.Empty,
        Slug = Convert.ToString(reader["Slug"]) ?? string.Empty,
        Category = Convert.ToString(reader["Category"]) ?? string.Empty,
        Excerpt = Convert.ToString(reader["Excerpt"]) ?? string.Empty,
        Content = Convert.ToString(reader["Content"]) ?? string.Empty,
        ImageUrl = reader["ImageUrl"] == DBNull.Value
            ? null : Convert.ToString(reader["ImageUrl"]),
        Author = Convert.ToString(reader["Author"]) ?? string.Empty,
        IsPublished = Convert.ToBoolean(reader["IsPublished"]),
        PublishedAt = reader["PublishedAt"] == DBNull.Value
            ? null : Convert.ToDateTime(reader["PublishedAt"]),
        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
        UpdatedAt = reader["UpdatedAt"] == DBNull.Value
            ? null : Convert.ToDateTime(reader["UpdatedAt"])
    };


    // Add Blog SQL Parameters
    private static void AddBlogParameters(
        SqlCommand cmd, BlogModel model, bool includeId)
    {
        if (includeId)
        {
            cmd.Parameters.Add("@BlogId", SqlDbType.Int)
                .Value = model.BlogId;
        }

        cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 200)
            .Value = model.Title.Trim();

        cmd.Parameters.Add("@Slug", SqlDbType.NVarChar, 220)
            .Value = model.Slug.Trim().ToLowerInvariant();

        cmd.Parameters.Add("@Category", SqlDbType.NVarChar, 100)
            .Value = model.Category?.Trim() ?? string.Empty;

        cmd.Parameters.Add("@Excerpt", SqlDbType.NVarChar, 500)
            .Value = model.Excerpt?.Trim() ?? string.Empty;

        cmd.Parameters.Add("@Content", SqlDbType.NVarChar, -1)
            .Value = model.Content;

        cmd.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 1000)
            .Value = (object?)model.ImageUrl?.Trim() ?? DBNull.Value;

        cmd.Parameters.Add("@Author", SqlDbType.NVarChar, 150)
            .Value = string.IsNullOrWhiteSpace(model.Author)
                ? "Airborne Flight Desk"
                : model.Author.Trim();

        cmd.Parameters.Add("@IsPublished", SqlDbType.Bit)
            .Value = model.IsPublished;
    }


    // ==========================================
    // 1. PUBLIC GET - PUBLISHED BLOGS
    // ==========================================

    // GET /api/Blog
    [HttpGet]
    public async Task<IActionResult> GetPublished()
    {
        try
        {
            var blogs = new List<BlogModel>();

            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.GetPublishedBlogs", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            await con.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                blogs.Add(ReadBlog(reader));
            }

            return Ok(blogs);
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Blogs load nahi ho sake. Database setup script run karein."
            });
        }
    }


    // ==========================================
    // 2. PUBLIC GET - BLOG BY SLUG
    // ==========================================

    // GET /api/Blog/{slug}
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
        {
            return BadRequest(new
            {
                message = "Valid blog slug required."
            });
        }

        try
        {
            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.GetPublishedBlogBySlug", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@Slug", SqlDbType.NVarChar, 220)
                .Value = slug.Trim().ToLowerInvariant();

            await con.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    message = "Published blog not found."
                });
            }

            return Ok(ReadBlog(reader));
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Blog load nahi ho saka."
            });
        }
    }


    // ==========================================
    // 3. ADMIN GET ALL - PUBLISHED + DRAFT BLOGS
    // ==========================================

    // GET /api/Blog/admin/all
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllForAdmin(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        // Verify Admin API Key
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
        {
            return denied;
        }

        try
        {
            var blogs = new List<BlogModel>();

            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.GetAllBlogs", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            await con.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                blogs.Add(ReadBlog(reader));
            }

            return Ok(blogs);
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Admin blog list load nahi hui."
            });
        }
    }


    // ==========================================
    // 4. ADMIN POST - CREATE BLOG
    // ==========================================

    // POST /api/Blog
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromBody] BlogModel model)
    {
        // Verify Admin API Key
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
        {
            return denied;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Slug) ||
            string.IsNullOrWhiteSpace(model.Content))
        {
            return BadRequest(new
            {
                message = "Title, Slug aur Content required hain."
            });
        }

        try
        {
            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.CreateBlog", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            AddBlogParameters(cmd, model, includeId: false);

            await con.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return StatusCode(500, new
                {
                    message = "Blog create nahi hua."
                });
            }

            var saved = ReadBlog(reader);

            return CreatedAtAction(
                nameof(GetBySlug),
                new { slug = saved.Slug },
                saved
            );
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                message = "Ye slug pehle se use ho raha hai. Unique slug dein."
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Blog save nahi ho saka."
            });
        }
    }


    // ==========================================
    // 5. ADMIN PUT - UPDATE BLOG
    // ==========================================

    // PUT /api/Blog/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromBody] BlogModel model)
    {
        // Verify Admin API Key
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
        {
            return denied;
        }

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid BlogId required."
            });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Slug) ||
            string.IsNullOrWhiteSpace(model.Content))
        {
            return BadRequest(new
            {
                message = "Title, Slug aur Content required hain."
            });
        }

        model.BlogId = id;

        try
        {
            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.UpdateBlog", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            AddBlogParameters(cmd, model, includeId: true);

            await con.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    message = "Blog nahi mila."
                });
            }

            return Ok(ReadBlog(reader));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                message = "Ye slug pehle se use ho raha hai."
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Blog update nahi ho saka."
            });
        }
    }


    // ==========================================
    // 6. ADMIN DELETE - DELETE BLOG
    // ==========================================

    // DELETE /api/Blog/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        // Verify Admin API Key
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
        {
            return denied;
        }

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid BlogId required."
            });
        }

        try
        {
            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.DeleteBlog", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@BlogId", SqlDbType.Int)
                .Value = id;

            await con.OpenAsync();

            var affected = Convert.ToInt32(
                await cmd.ExecuteScalarAsync());

            if (affected == 0)
            {
                return NotFound(new
                {
                    message = "Blog nahi mila."
                });
            }

            return Ok(new
            {
                message = "Blog delete ho gaya.",
                blogId = id
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Blog delete nahi ho saka."
            });
        }
    }
}
