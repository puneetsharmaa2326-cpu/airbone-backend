using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    private const long MaxImageSize = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public BlogController(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    // ==========================================
    // DATABASE CONNECTION
    // ==========================================

    private SqlConnection CreateConnection() => new(
        _configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is not configured."));


    // ==========================================
    // ADMIN API KEY VALIDATION
    // ==========================================

    private bool AdminKeyIsValid(string? suppliedKey = null)
    {
        var configuredKey = _configuration["BlogAdmin:ApiKey"];

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

    private IActionResult? RequireAdminKey(string? suppliedKey = null)
    {
        var configuredKey = _configuration["BlogAdmin:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(503, new
            {
                message = "Blog admin API key is not configured on the server."
            });
        }

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
    // IMAGE VALIDATION
    // ==========================================

    private string? ValidateImage(IFormFile? image)
    {
        if (image == null || image.Length == 0)
            return null;

        if (image.Length > MaxImageSize)
        {
            return "Image size maximum 5 MB allowed hai.";
        }

        var extension = Path.GetExtension(image.FileName)
            .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return "Only JPG, JPEG, PNG and WEBP images allowed hain.";
        }

        var allowedTypes = new Dictionary<string, string>
        {
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".webp", "image/webp" }
        };

        if (!allowedTypes.TryGetValue(extension, out var expectedType) ||
            !string.Equals(
                image.ContentType,
                expectedType,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Image file type valid nahi hai.";
        }

        return null;
    }


    // ==========================================
    // SAVE IMAGE TO SERVER
    // ==========================================

    private async Task<string?> SaveBlogImageAsync(IFormFile? image)
    {
        if (image == null || image.Length == 0)
            return null;

        var extension = Path.GetExtension(image.FileName)
            .ToLowerInvariant();

        var folderPath = Path.Combine(
            _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            "blogs"
        );

        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";

        var filePath = Path.Combine(folderPath, fileName);

        await using (var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await image.CopyToAsync(stream);
        }

        return $"{Request.Scheme}://{Request.Host}/uploads/blogs/{fileName}";
    }


    // ==========================================
    // DELETE LOCAL BLOG IMAGE
    // ==========================================

    private void DeleteBlogImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return;

        try
        {
            var uri = new Uri(imageUrl, UriKind.Absolute);

            if (!string.Equals(
                uri.Host,
                Request.Host.Host,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            const string prefix = "/uploads/blogs/";

            if (!uri.AbsolutePath.StartsWith(
                prefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(uri.AbsolutePath);

            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var root = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            var filePath = Path.Combine(
                root, "uploads", "blogs", fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch
        {
            // Image deletion failure should not break blog operations.
        }
    }


    // ==========================================
    // READ BLOG DATA
    // ==========================================

    private static BlogModel ReadBlog(SqlDataReader reader) => new()
    {
        BlogId = Convert.ToInt32(reader["BlogId"]),
        Title = Convert.ToString(reader["Title"]) ?? string.Empty,
        Slug = Convert.ToString(reader["Slug"]) ?? string.Empty,
        Category = Convert.ToString(reader["Category"]) ?? string.Empty,
        Excerpt = Convert.ToString(reader["Excerpt"]) ?? string.Empty,
        Content = Convert.ToString(reader["Content"]) ?? string.Empty,

        ImageUrl = reader["ImageUrl"] == DBNull.Value
            ? null
            : Convert.ToString(reader["ImageUrl"]),

        Author = Convert.ToString(reader["Author"]) ?? string.Empty,

        IsPublished = Convert.ToBoolean(reader["IsPublished"]),

        PublishedAt = reader["PublishedAt"] == DBNull.Value
            ? null
            : Convert.ToDateTime(reader["PublishedAt"]),

        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),

        UpdatedAt = reader["UpdatedAt"] == DBNull.Value
            ? null
            : Convert.ToDateTime(reader["UpdatedAt"])
    };


    // ==========================================
    // ADD SQL PARAMETERS
    // ==========================================

    private static void AddBlogParameters(
        SqlCommand cmd,
        BlogModel model,
        bool includeId)
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
    // GET EXISTING BLOG IMAGE URL
    // ==========================================

    private async Task<string?> GetExistingImageUrlAsync(int blogId)
    {
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
            if (Convert.ToInt32(reader["BlogId"]) == blogId)
            {
                return reader["ImageUrl"] == DBNull.Value
                    ? null
                    : Convert.ToString(reader["ImageUrl"]);
            }
        }

        return null;
    }


    // ==========================================
    // GET COMPLETE BLOG BY ID
    // ==========================================

    private async Task<BlogModel?> GetBlogByIdAsync(int blogId)
    {
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
            if (Convert.ToInt32(reader["BlogId"]) == blogId)
            {
                return ReadBlog(reader);
            }
        }

        return null;
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
    // 3. ADMIN GET ALL BLOGS
    // ==========================================

    // GET /api/Blog/admin/all

    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllForAdmin(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
            return denied;

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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromForm] BlogModel model)
    {
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
            return denied;

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Slug) ||
            string.IsNullOrWhiteSpace(model.Content))
        {
            return BadRequest(new
            {
                message = "Title, Slug aur Content required hain."
            });
        }

        var imageError = ValidateImage(model.Image);

        if (imageError != null)
        {
            return BadRequest(new { message = imageError });
        }

        string? uploadedImageUrl = null;

        try
        {
            if (model.Image != null)
            {
                uploadedImageUrl = await SaveBlogImageAsync(model.Image);
                model.ImageUrl = uploadedImageUrl;
            }

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
                DeleteBlogImage(uploadedImageUrl);

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
            DeleteBlogImage(uploadedImageUrl);

            return Conflict(new
            {
                message = "Ye slug pehle se use ho raha hai. Unique slug dein."
            });
        }
        catch (SqlException)
        {
            DeleteBlogImage(uploadedImageUrl);

            return StatusCode(500, new
            {
                message = "Blog save nahi ho saka."
            });
        }
        catch (Exception)
        {
            DeleteBlogImage(uploadedImageUrl);

            return StatusCode(500, new
            {
                message = "Blog create karte waqt error aaya."
            });
        }
    }


    // ==========================================
    // 5. ADMIN PUT - UPDATE BLOG
    // ==========================================

    // PUT /api/Blog/{id}

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromForm] BlogModel model)
    {
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
            return denied;

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid BlogId required."
            });
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Slug) ||
            string.IsNullOrWhiteSpace(model.Content))
        {
            return BadRequest(new
            {
                message = "Title, Slug aur Content required hain."
            });
        }

        var imageError = ValidateImage(model.Image);

        if (imageError != null)
            return BadRequest(new { message = imageError });

        model.BlogId = id;

        string? oldImageUrl = null;
        string? uploadedImageUrl = null;

        try
        {
            oldImageUrl = await GetExistingImageUrlAsync(id);

            if (model.Image != null)
            {
                uploadedImageUrl = await SaveBlogImageAsync(model.Image);
                model.ImageUrl = uploadedImageUrl;
            }
            else
            {
                model.ImageUrl = oldImageUrl;
            }

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
                DeleteBlogImage(uploadedImageUrl);

                return NotFound(new
                {
                    message = "Blog nahi mila."
                });
            }

            var updated = ReadBlog(reader);

            if (uploadedImageUrl != null &&
                !string.Equals(oldImageUrl, uploadedImageUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                DeleteBlogImage(oldImageUrl);
            }

            return Ok(updated);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            DeleteBlogImage(uploadedImageUrl);

            return Conflict(new
            {
                message = "Ye slug pehle se use ho raha hai."
            });
        }
        catch (SqlException)
        {
            DeleteBlogImage(uploadedImageUrl);

            return StatusCode(500, new
            {
                message = "Blog update nahi ho saka."
            });
        }
        catch (Exception)
        {
            DeleteBlogImage(uploadedImageUrl);

            return StatusCode(500, new
            {
                message = "Blog update karte waqt error aaya."
            });
        }
    }


    // ==========================================
    // 6. ADMIN PATCH - CHANGE BLOG STATUS
    // ==========================================

    // PATCH /api/Blog/{id}/status?isPublished=true
    // PATCH /api/Blog/{id}/status?isPublished=false

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateBlogStatus(
        int id,
        [FromQuery] bool isPublished,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
            return denied;

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid BlogId required."
            });
        }

        try
        {
            // Get existing blog with all its details
            var blog = await GetBlogByIdAsync(id);

            if (blog == null)
            {
                return NotFound(new
                {
                    message = "Blog nahi mila."
                });
            }

            // Change only publication status
            blog.IsPublished = isPublished;

            // Use existing UpdateBlog stored procedure
            await using var con = CreateConnection();

            await using var cmd = new SqlCommand(
                "dbo.UpdateBlog", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            AddBlogParameters(cmd, blog, includeId: true);

            await con.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    message = "Blog status update nahi hua."
                });
            }

            var updatedBlog = ReadBlog(reader);

            return Ok(new
            {
                success = true,
                message = isPublished
                    ? "Blog successfully Published ho gaya."
                    : "Blog successfully Draft ho gaya.",
                blog = updatedBlog
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                message = "Database mein blog status update nahi ho saka."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                message = "Blog status change karte waqt error aaya."
            });
        }
    }


    // ==========================================
    // 7. ADMIN DELETE - DELETE BLOG
    // ==========================================

    // DELETE /api/Blog/{id}

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        var denied = RequireAdminKey(adminKey);

        if (denied != null)
            return denied;

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid BlogId required."
            });
        }

        try
        {
            var oldImageUrl = await GetExistingImageUrlAsync(id);

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

            DeleteBlogImage(oldImageUrl);

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
        catch (Exception)
        {
            return StatusCode(500, new
            {
                message = "Blog delete karte waqt error aaya."
            });
        }
    }
}