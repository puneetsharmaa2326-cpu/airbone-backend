
//using System.Data;
//using System.Security.Cryptography;
//using System.Text;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Data.SqlClient;
//using CloudinaryDotNet;
//using CloudinaryDotNet.Actions;
//using WebApplication1.Models;

//namespace WebApplication1.Controllers;

//[ApiController]
//[Route("api/[controller]")]
//public class BlogController : ControllerBase
//{
//    private readonly IConfiguration _configuration;
//    private readonly IWebHostEnvironment _environment;

//    private const long MaxImageSize = 5 * 1024 * 1024;

//    private static readonly string[] AllowedExtensions =
//    {
//        ".jpg", ".jpeg", ".png", ".webp"
//    };

//    public BlogController(
//        IConfiguration configuration,
//        IWebHostEnvironment environment)
//    {
//        _configuration = configuration;
//        _environment = environment;
//    }

//    // ==========================================
//    // DATABASE CONNECTION
//    // ==========================================

//    private SqlConnection CreateConnection() => new(
//        _configuration.GetConnectionString("DefaultConnection")
//        ?? throw new InvalidOperationException(
//            "The database connection string is not configured."));


//    // ==========================================
//    // CLOUDINARY CONNECTION
//    // ==========================================

//    private Cloudinary CreateCloudinary()
//    {
//        var cloudName = _configuration["Cloudinary:CloudName"];
//        var apiKey = _configuration["Cloudinary:ApiKey"];
//        var apiSecret = _configuration["Cloudinary:ApiSecret"];

//        if (string.IsNullOrWhiteSpace(cloudName) ||
//            string.IsNullOrWhiteSpace(apiKey) ||
//            string.IsNullOrWhiteSpace(apiSecret))
//        {
//            throw new InvalidOperationException(
//                "Cloudinary configuration is missing. Please check the server configuration.");
//        }

//        var account = new Account(
//            cloudName,
//            apiKey,
//            apiSecret
//        );

//        var cloudinary = new CloudinaryDotNet.Cloudinary(account);

//        cloudinary.Api.Secure = true;

//        return cloudinary;
//    }


//    // ==========================================
//    // ADMIN API KEY VALIDATION
//    // ==========================================

//    private bool AdminKeyIsValid(string? suppliedKey = null)
//    {
//        var configuredKey = _configuration["BlogAdmin:ApiKey"];

//        if (string.IsNullOrWhiteSpace(suppliedKey))
//        {
//            suppliedKey = Request.Headers["X-Admin-Key"].ToString();
//        }

//        if (string.IsNullOrWhiteSpace(configuredKey) ||
//            string.IsNullOrWhiteSpace(suppliedKey))
//        {
//            return false;
//        }

//        var expected = Encoding.UTF8.GetBytes(configuredKey);
//        var actual = Encoding.UTF8.GetBytes(suppliedKey);

//        return expected.Length == actual.Length &&
//               CryptographicOperations.FixedTimeEquals(
//                   expected, actual);
//    }

//    private IActionResult? RequireAdminKey(string? suppliedKey = null)
//    {
//        var configuredKey = _configuration["BlogAdmin:ApiKey"];

//        if (string.IsNullOrWhiteSpace(configuredKey))
//        {
//            return StatusCode(503, new
//            {
//                success = false,
//                message = "Blog administration is currently unavailable. The server API key has not been configured."
//            });
//        }

//        if (!AdminKeyIsValid(suppliedKey))
//        {
//            return Unauthorized(new
//            {
//                success = false,
//                message = "Access denied. A valid administrator API key is required."
//            });
//        }

//        return null;
//    }


//    // ==========================================
//    // IMAGE VALIDATION
//    // ==========================================

//    private string? ValidateImage(IFormFile? image)
//    {
//        if (image == null || image.Length == 0)
//            return null;

//        if (image.Length > MaxImageSize)
//        {
//            return "The uploaded image exceeds the maximum allowed size of 5 MB.";
//        }

//        var extension = Path.GetExtension(image.FileName)
//            .ToLowerInvariant();

//        if (!AllowedExtensions.Contains(extension))
//        {
//            return "Invalid image format. Only JPG, JPEG, PNG, and WEBP files are supported.";
//        }

//        var allowedTypes = new Dictionary<string, string>
//        {
//            { ".jpg", "image/jpeg" },
//            { ".jpeg", "image/jpeg" },
//            { ".png", "image/png" },
//            { ".webp", "image/webp" }
//        };

//        if (!allowedTypes.TryGetValue(extension, out var expectedType) ||
//            !string.Equals(
//                image.ContentType,
//                expectedType,
//                StringComparison.OrdinalIgnoreCase))
//        {
//            return "The uploaded file does not have a valid image content type.";
//        }

//        return null;
//    }


//    // ==========================================
//    // UPLOAD IMAGE TO CLOUDINARY
//    // ==========================================

//    private async Task<string?> SaveBlogImageAsync(IFormFile? image)
//    {
//        if (image == null || image.Length == 0)
//            return null;

//        var cloudinary = CreateCloudinary();

//        await using var stream = image.OpenReadStream();

//        var uploadParams = new ImageUploadParams
//        {
//            File = new FileDescription(
//                image.FileName,
//                stream
//            ),

//            Folder = "airborne/blogs",

//            PublicId = Guid.NewGuid().ToString("N"),

//            Overwrite = false
//        };

//        var uploadResult = await cloudinary.UploadAsync(uploadParams);

//        if (uploadResult.Error != null)
//        {
//            throw new Exception(
//                "Cloudinary image upload failed: " +
//                uploadResult.Error.Message
//            );
//        }

//        if (uploadResult.SecureUrl == null)
//        {
//            throw new Exception(
//                "Cloudinary did not return a valid secure image URL.");
//        }

//        return uploadResult.SecureUrl.ToString();
//    }


//    // ==========================================
//    // DELETE OLD LOCAL BLOG IMAGE
//    // ==========================================

//    private void DeleteBlogImage(string? imageUrl)
//    {
//        if (string.IsNullOrWhiteSpace(imageUrl))
//            return;

//        try
//        {
//            var uri = new Uri(imageUrl, UriKind.Absolute);

//            // Do not delete Cloudinary or external images.
//            if (!string.Equals(
//                uri.Host,
//                Request.Host.Host,
//                StringComparison.OrdinalIgnoreCase))
//            {
//                return;
//            }

//            const string prefix = "/uploads/blogs/";

//            if (!uri.AbsolutePath.StartsWith(
//                prefix, StringComparison.OrdinalIgnoreCase))
//            {
//                return;
//            }

//            var fileName = Path.GetFileName(uri.AbsolutePath);

//            if (string.IsNullOrWhiteSpace(fileName))
//                return;

//            var root = _environment.WebRootPath
//                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

//            var filePath = Path.Combine(
//                root, "uploads", "blogs", fileName);

//            if (System.IO.File.Exists(filePath))
//            {
//                System.IO.File.Delete(filePath);
//            }
//        }
//        catch
//        {
//            // Image cleanup failures must not interrupt blog operations.
//        }
//    }


//    // ==========================================
//    // READ BLOG DATA
//    // ==========================================

//    private static BlogModel ReadBlog(SqlDataReader reader) => new()
//    {
//        BlogId = Convert.ToInt32(reader["BlogId"]),

//        Title = Convert.ToString(reader["Title"]) ?? string.Empty,

//        Slug = Convert.ToString(reader["Slug"]) ?? string.Empty,

//        Category = Convert.ToString(reader["Category"]) ?? string.Empty,

//        Excerpt = Convert.ToString(reader["Excerpt"]) ?? string.Empty,

//        Content = Convert.ToString(reader["Content"]) ?? string.Empty,

//        ImageUrl = reader["ImageUrl"] == DBNull.Value
//            ? null
//            : Convert.ToString(reader["ImageUrl"]),

//        Author = Convert.ToString(reader["Author"]) ?? string.Empty,

//        IsPublished = Convert.ToBoolean(reader["IsPublished"]),

//        PublishedAt = reader["PublishedAt"] == DBNull.Value
//            ? null
//            : Convert.ToDateTime(reader["PublishedAt"]),

//        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),

//        UpdatedAt = reader["UpdatedAt"] == DBNull.Value
//            ? null
//            : Convert.ToDateTime(reader["UpdatedAt"])
//    };


//    // ==========================================
//    // ADD SQL PARAMETERS
//    // ==========================================

//    private static void AddBlogParameters(
//        SqlCommand cmd,
//        BlogModel model,
//        bool includeId)
//    {
//        if (includeId)
//        {
//            cmd.Parameters.Add("@BlogId", SqlDbType.Int)
//                .Value = model.BlogId;
//        }

//        cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 200)
//            .Value = model.Title.Trim();

//        cmd.Parameters.Add("@Slug", SqlDbType.NVarChar, 220)
//            .Value = model.Slug.Trim().ToLowerInvariant();

//        cmd.Parameters.Add("@Category", SqlDbType.NVarChar, 100)
//            .Value = model.Category?.Trim() ?? string.Empty;

//        cmd.Parameters.Add("@Excerpt", SqlDbType.NVarChar, 500)
//            .Value = model.Excerpt?.Trim() ?? string.Empty;

//        cmd.Parameters.Add("@Content", SqlDbType.NVarChar, -1)
//            .Value = model.Content;

//        cmd.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 1000)
//            .Value = (object?)model.ImageUrl?.Trim() ?? DBNull.Value;

//        cmd.Parameters.Add("@Author", SqlDbType.NVarChar, 150)
//            .Value = string.IsNullOrWhiteSpace(model.Author)
//                ? "Airborne Flight Desk"
//                : model.Author.Trim();

//        cmd.Parameters.Add("@IsPublished", SqlDbType.Bit)
//            .Value = model.IsPublished;
//    }


//    // ==========================================
//    // GET EXISTING BLOG IMAGE URL
//    // ==========================================

//    private async Task<string?> GetExistingImageUrlAsync(int blogId)
//    {
//        await using var con = CreateConnection();

//        await using var cmd = new SqlCommand(
//            "dbo.GetAllBlogs", con)
//        {
//            CommandType = CommandType.StoredProcedure
//        };

//        await con.OpenAsync();

//        await using var reader = await cmd.ExecuteReaderAsync();

//        while (await reader.ReadAsync())
//        {
//            if (Convert.ToInt32(reader["BlogId"]) == blogId)
//            {
//                return reader["ImageUrl"] == DBNull.Value
//                    ? null
//                    : Convert.ToString(reader["ImageUrl"]);
//            }
//        }

//        return null;
//    }


//    // ==========================================
//    // GET COMPLETE BLOG BY ID
//    // ==========================================

//    private async Task<BlogModel?> GetBlogByIdAsync(int blogId)
//    {
//        await using var con = CreateConnection();

//        await using var cmd = new SqlCommand(
//            "dbo.GetAllBlogs", con)
//        {
//            CommandType = CommandType.StoredProcedure
//        };

//        await con.OpenAsync();

//        await using var reader = await cmd.ExecuteReaderAsync();

//        while (await reader.ReadAsync())
//        {
//            if (Convert.ToInt32(reader["BlogId"]) == blogId)
//            {
//                return ReadBlog(reader);
//            }
//        }

//        return null;
//    }


//    // ==========================================
//    // 1. PUBLIC GET - PUBLISHED BLOGS
//    // ==========================================

//    // GET /api/Blog

//    [HttpGet]
//    public async Task<IActionResult> GetPublished()
//    {
//        try
//        {
//            var blogs = new List<BlogModel>();

//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.GetPublishedBlogs", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            await con.OpenAsync();

//            await using var reader = await cmd.ExecuteReaderAsync();

//            while (await reader.ReadAsync())
//            {
//                blogs.Add(ReadBlog(reader));
//            }

//            return Ok(blogs);
//        }
//        catch (SqlException)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to retrieve published blogs. Please try again later."
//            });
//        }
//    }


//    // ==========================================
//    // 2. PUBLIC GET - BLOG BY SLUG
//    // ==========================================

//    // GET /api/Blog/{slug}

//    [HttpGet("{slug}")]
//    public async Task<IActionResult> GetBySlug(string slug)
//    {
//        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = "Please provide a valid blog slug."
//            });
//        }

//        try
//        {
//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.GetPublishedBlogBySlug", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            cmd.Parameters.Add("@Slug", SqlDbType.NVarChar, 220)
//                .Value = slug.Trim().ToLowerInvariant();

//            await con.OpenAsync();

//            await using var reader = await cmd.ExecuteReaderAsync();

//            if (!await reader.ReadAsync())
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "The requested published blog could not be found."
//                });
//            }

//            return Ok(ReadBlog(reader));
//        }
//        catch (SqlException)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to retrieve the requested blog. Please try again later."
//            });
//        }
//    }


//    // ==========================================
//    // 3. ADMIN GET ALL BLOGS
//    // ==========================================

//    // GET /api/Blog/admin/all

//    [HttpGet("admin/all")]
//    public async Task<IActionResult> GetAllForAdmin(
//        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
//    {
//        var denied = RequireAdminKey(adminKey);

//        if (denied != null)
//            return denied;

//        try
//        {
//            var blogs = new List<BlogModel>();

//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.GetAllBlogs", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            await con.OpenAsync();

//            await using var reader = await cmd.ExecuteReaderAsync();

//            while (await reader.ReadAsync())
//            {
//                blogs.Add(ReadBlog(reader));
//            }

//            return Ok(blogs);
//        }
//        catch (SqlException)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to retrieve the blog management list. Please try again later."
//            });
//        }
//    }


//    // ==========================================
//    // 4. ADMIN POST - CREATE BLOG
//    // ==========================================

//    // POST /api/Blog

//    [HttpPost]
//    [Consumes("multipart/form-data")]
//    public async Task<IActionResult> Create(
//        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
//        [FromForm] BlogModel model)
//    {
//        var denied = RequireAdminKey(adminKey);

//        if (denied != null)
//            return denied;

//        if (!ModelState.IsValid)
//            return ValidationProblem(ModelState);

//        if (string.IsNullOrWhiteSpace(model.Title) ||
//            string.IsNullOrWhiteSpace(model.Slug) ||
//            string.IsNullOrWhiteSpace(model.Content))
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = "Title, slug, and content are required fields."
//            });
//        }

//        var imageError = ValidateImage(model.Image);

//        if (imageError != null)
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = imageError
//            });
//        }

//        string? uploadedImageUrl = null;

//        try
//        {
//            if (model.Image != null)
//            {
//                uploadedImageUrl = await SaveBlogImageAsync(model.Image);

//                model.ImageUrl = uploadedImageUrl;
//            }

//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.CreateBlog", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            AddBlogParameters(cmd, model, includeId: false);

//            await con.OpenAsync();

//            await using var reader = await cmd.ExecuteReaderAsync();

//            if (!await reader.ReadAsync())
//            {
//                DeleteBlogImage(uploadedImageUrl);

//                return StatusCode(500, new
//                {
//                    success = false,
//                    message = "The blog could not be created. Please try again."
//                });
//            }

//            var saved = ReadBlog(reader);

//            return CreatedAtAction(
//                nameof(GetBySlug),
//                new { slug = saved.Slug },
//                new
//                {
//                    success = true,
//                    message = "Blog created successfully.",
//                    blog = saved
//                }
//            );
//        }
//        catch (SqlException ex) when (ex.Number is 2601 or 2627)
//        {
//            DeleteBlogImage(uploadedImageUrl);

//            return Conflict(new
//            {
//                success = false,
//                message = "This blog slug already exists. Please use a unique slug."
//            });
//        }
//        catch (SqlException)
//        {
//            DeleteBlogImage(uploadedImageUrl);

//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to save the blog due to a database error. Please try again later."
//            });
//        }
//        catch (Exception)
//        {
//            DeleteBlogImage(uploadedImageUrl);

//            return StatusCode(500, new
//            {
//                success = false,
//                message = "An error occurred while creating the blog. Please verify the image upload configuration and try again."
//            });
//        }
//    }


//    // ==========================================
//    // 5. ADMIN PUT - UPDATE BLOG
//    // ==========================================

//    // PUT /api/Blog/{id}

//    [HttpPut("{id:int}")]
//    [Consumes("multipart/form-data")]
//    public async Task<IActionResult> Update(
//        int id,
//        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
//        [FromForm] BlogModel model)
//    {
//        var denied = RequireAdminKey(adminKey);

//        if (denied != null)
//            return denied;

//        if (id <= 0)
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = "Please provide a valid blog ID."
//            });
//        }

//        if (!ModelState.IsValid)
//            return ValidationProblem(ModelState);

//        if (string.IsNullOrWhiteSpace(model.Title) ||
//            string.IsNullOrWhiteSpace(model.Slug) ||
//            string.IsNullOrWhiteSpace(model.Content))
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = "Title, slug, and content are required fields."
//            });
//        }

//        var imageError = ValidateImage(model.Image);

//        if (imageError != null)
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = imageError
//            });
//        }

//        model.BlogId = id;

//        string? oldImageUrl = null;
//        string? uploadedImageUrl = null;

//        try
//        {
//            oldImageUrl = await GetExistingImageUrlAsync(id);

//            if (model.Image != null)
//            {
//                uploadedImageUrl = await SaveBlogImageAsync(model.Image);

//                model.ImageUrl = uploadedImageUrl;
//            }
//            else
//            {
//                model.ImageUrl = oldImageUrl;
//            }

//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.UpdateBlog", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            AddBlogParameters(cmd, model, includeId: true);

//            await con.OpenAsync();

//            await using var reader = await cmd.ExecuteReaderAsync();

//            if (!await reader.ReadAsync())
//            {
//                DeleteBlogImage(uploadedImageUrl);

//                return NotFound(new
//                {
//                    success = false,
//                    message = "The blog you are trying to update could not be found."
//                });
//            }

//            var updated = ReadBlog(reader);

//            if (uploadedImageUrl != null &&
//                !string.Equals(
//                    oldImageUrl,
//                    uploadedImageUrl,
//                    StringComparison.OrdinalIgnoreCase))
//            {
//                DeleteBlogImage(oldImageUrl);
//            }

//            return Ok(new
//            {
//                success = true,
//                message = "Blog updated successfully.",
//                blog = updated
//            });
//        }
//        catch (SqlException ex) when (ex.Number is 2601 or 2627)
//        {
//            DeleteBlogImage(uploadedImageUrl);

//            return Conflict(new
//            {
//                success = false,
//                message = "This blog slug is already in use. Please choose a different slug."
//            });
//        }
//        catch (SqlException)
//        {
//            DeleteBlogImage(uploadedImageUrl);

//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to update the blog due to a database error. Please try again later."
//            });
//        }
//        catch (Exception)
//        {
//            DeleteBlogImage(uploadedImageUrl);

//            return StatusCode(500, new
//            {
//                success = false,
//                message = "An error occurred while updating the blog. Please verify the image upload configuration and try again."
//            });
//        }
//    }


//    // ==========================================
//    // 6. ADMIN PATCH - CHANGE BLOG STATUS
//    // ==========================================

//    // PATCH /api/Blog/{id}/status?isPublished=true
//    // PATCH /api/Blog/{id}/status?isPublished=false

//    [HttpPatch("{id:int}/status")]
//    public async Task<IActionResult> UpdateBlogStatus(
//        int id,
//        [FromQuery] bool isPublished,
//        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
//    {
//        var denied = RequireAdminKey(adminKey);

//        if (denied != null)
//            return denied;

//        if (id <= 0)
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = "Please provide a valid blog ID."
//            });
//        }

//        try
//        {
//            var blog = await GetBlogByIdAsync(id);

//            if (blog == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "The requested blog could not be found."
//                });
//            }

//            blog.IsPublished = isPublished;

//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.UpdateBlog", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            AddBlogParameters(cmd, blog, includeId: true);

//            await con.OpenAsync();

//            await using var reader = await cmd.ExecuteReaderAsync();

//            if (!await reader.ReadAsync())
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "The blog status could not be updated because the blog was not found."
//                });
//            }

//            var updatedBlog = ReadBlog(reader);

//            return Ok(new
//            {
//                success = true,
//                message = isPublished
//                    ? "Blog published successfully."
//                    : "Blog moved to draft successfully.",
//                blog = updatedBlog
//            });
//        }
//        catch (SqlException)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to update the blog status due to a database error."
//            });
//        }
//        catch (Exception)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "An unexpected error occurred while changing the blog status."
//            });
//        }
//    }


//    // ==========================================
//    // 7. ADMIN DELETE - DELETE BLOG
//    // ==========================================

//    // DELETE /api/Blog/{id}

//    [HttpDelete("{id:int}")]
//    public async Task<IActionResult> Delete(
//        int id,
//        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
//    {
//        var denied = RequireAdminKey(adminKey);

//        if (denied != null)
//            return denied;

//        if (id <= 0)
//        {
//            return BadRequest(new
//            {
//                success = false,
//                message = "Please provide a valid blog ID."
//            });
//        }

//        try
//        {
//            var oldImageUrl = await GetExistingImageUrlAsync(id);

//            await using var con = CreateConnection();

//            await using var cmd = new SqlCommand(
//                "dbo.DeleteBlog", con)
//            {
//                CommandType = CommandType.StoredProcedure
//            };

//            cmd.Parameters.Add("@BlogId", SqlDbType.Int)
//                .Value = id;

//            await con.OpenAsync();

//            var affected = Convert.ToInt32(
//                await cmd.ExecuteScalarAsync());

//            if (affected == 0)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "The blog you are trying to delete could not be found."
//                });
//            }

//            DeleteBlogImage(oldImageUrl);

//            return Ok(new
//            {
//                success = true,
//                message = "Blog deleted successfully.",
//                blogId = id
//            });
//        }
//        catch (SqlException)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "Unable to delete the blog due to a database error. Please try again later."
//            });
//        }
//        catch (Exception)
//        {
//            return StatusCode(500, new
//            {
//                success = false,
//                message = "An unexpected error occurred while deleting the blog."
//            });
//        }
//    }
//}

//---------------------------------------------------------------new CoDE START----------------------------------------------------------------------

using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    private const long MaxFileSize = 100 * 1024 * 1024; // 100 MB (videos ke liye)

    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp",   // Images
        ".mp4", ".webm", ".ogg"             // Videos
    };

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".webm", ".ogg"
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
            "The database connection string is not configured."));

    // ==========================================
    // CLOUDINARY CONNECTION
    // ==========================================

    private Cloudinary CreateCloudinary()
    {
        var cloudName = _configuration["Cloudinary:CloudName"];
        var apiKey = _configuration["Cloudinary:ApiKey"];
        var apiSecret = _configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException(
                "Cloudinary configuration is missing. Please check the server configuration.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        var cloudinary = new CloudinaryDotNet.Cloudinary(account);
        cloudinary.Api.Secure = true;
        return cloudinary;
    }

    // ==========================================
    // ADMIN API KEY VALIDATION
    // ==========================================

    private bool AdminKeyIsValid(string? suppliedKey = null)
    {
        var configuredKey = _configuration["BlogAdmin:ApiKey"];

        if (string.IsNullOrWhiteSpace(suppliedKey))
            suppliedKey = Request.Headers["X-Admin-Key"].ToString();

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            string.IsNullOrWhiteSpace(suppliedKey))
            return false;

        var expected = Encoding.UTF8.GetBytes(configuredKey);
        var actual = Encoding.UTF8.GetBytes(suppliedKey);

        return expected.Length == actual.Length &&
               CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private IActionResult? RequireAdminKey(string? suppliedKey = null)
    {
        var configuredKey = _configuration["BlogAdmin:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(503, new
            {
                success = false,
                message = "Blog administration is currently unavailable. The server API key has not been configured."
            });
        }

        if (!AdminKeyIsValid(suppliedKey))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Access denied. A valid administrator API key is required."
            });
        }

        return null;
    }

    // ==========================================
    // FILE VALIDATION (Image + Video)
    // ==========================================

    private string? ValidateMediaFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        if (file.Length > MaxFileSize)
            return $"The uploaded file \"{file.FileName}\" exceeds the maximum allowed size of 100 MB.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            return $"Invalid file format \"{extension}\". Allowed: JPG, JPEG, PNG, WEBP, MP4, WEBM, OGG.";

        return null;
    }

    // ==========================================
    // UPLOAD IMAGE OR VIDEO TO CLOUDINARY
    // ==========================================

    private async Task<string?> SaveMediaFileAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var cloudinary = CreateCloudinary();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isVideo = VideoExtensions.Contains(extension);

        await using var stream = file.OpenReadStream();

        if (isVideo)
        {
            // ── VIDEO UPLOAD ──────────────────────────────
            var videoParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "airborne/blogs",
                PublicId = Guid.NewGuid().ToString("N"),
                Overwrite = false
            };

            var videoResult = await cloudinary.UploadAsync(videoParams);

            if (videoResult.Error != null)
                throw new Exception("Cloudinary video upload failed: " + videoResult.Error.Message);

            if (videoResult.SecureUrl == null)
                throw new Exception("Cloudinary did not return a valid secure video URL.");

            return videoResult.SecureUrl.ToString();
            // ─────────────────────────────────────────────
        }
        else
        {
            // ── IMAGE UPLOAD ──────────────────────────────
            var imageParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "airborne/blogs",
                PublicId = Guid.NewGuid().ToString("N"),
                Overwrite = false
            };

            var imageResult = await cloudinary.UploadAsync(imageParams);

            if (imageResult.Error != null)
                throw new Exception("Cloudinary image upload failed: " + imageResult.Error.Message);

            if (imageResult.SecureUrl == null)
                throw new Exception("Cloudinary did not return a valid secure image URL.");

            return imageResult.SecureUrl.ToString();
            // ─────────────────────────────────────────────
        }
    }

    // ==========================================
    // DELETE OLD LOCAL BLOG IMAGE
    // ==========================================

    private void DeleteBlogImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return;

        try
        {
            var uri = new Uri(imageUrl, UriKind.Absolute);

            if (!string.Equals(uri.Host, Request.Host.Host,
                    StringComparison.OrdinalIgnoreCase))
                return;

            const string prefix = "/uploads/blogs/";

            if (!uri.AbsolutePath.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase))
                return;

            var fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var root = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            var filePath = Path.Combine(root, "uploads", "blogs", fileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
        catch
        {
            // Image cleanup failures must not interrupt blog operations.
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
            cmd.Parameters.Add("@BlogId", SqlDbType.Int).Value = model.BlogId;

        cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = model.Title.Trim();
        cmd.Parameters.Add("@Slug", SqlDbType.NVarChar, 220).Value = model.Slug.Trim().ToLowerInvariant();
        cmd.Parameters.Add("@Category", SqlDbType.NVarChar, 100).Value = model.Category?.Trim() ?? string.Empty;
        cmd.Parameters.Add("@Excerpt", SqlDbType.NVarChar, 500).Value = model.Excerpt?.Trim() ?? string.Empty;
        cmd.Parameters.Add("@Content", SqlDbType.NVarChar, -1).Value = model.Content;
        cmd.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 2000).Value = (object?)model.ImageUrl?.Trim() ?? DBNull.Value;
        cmd.Parameters.Add("@Author", SqlDbType.NVarChar, 150).Value =
            string.IsNullOrWhiteSpace(model.Author) ? "Airborne Flight Desk" : model.Author.Trim();
        cmd.Parameters.Add("@IsPublished", SqlDbType.Bit).Value = model.IsPublished;
    }

    // ==========================================
    // GET EXISTING BLOG IMAGE URL
    // ==========================================

    private async Task<string?> GetExistingImageUrlAsync(int blogId)
    {
        await using var con = CreateConnection();
        await using var cmd = new SqlCommand("dbo.GetAllBlogs", con)
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
        await using var cmd = new SqlCommand("dbo.GetAllBlogs", con)
        {
            CommandType = CommandType.StoredProcedure
        };

        await con.OpenAsync();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (Convert.ToInt32(reader["BlogId"]) == blogId)
                return ReadBlog(reader);
        }

        return null;
    }

    // ==========================================
    // 1. PUBLIC GET - PUBLISHED BLOGS
    // GET /api/Blog
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> GetPublished()
    {
        try
        {
            var blogs = new List<BlogModel>();

            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.GetPublishedBlogs", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                blogs.Add(ReadBlog(reader));

            return Ok(blogs);
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to retrieve published blogs. Please try again later."
            });
        }
    }

    // ==========================================
    // 2. PUBLIC GET - BLOG BY SLUG
    // GET /api/Blog/{slug}
    // ==========================================

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
        {
            return BadRequest(new
            {
                success = false,
                message = "Please provide a valid blog slug."
            });
        }

        try
        {
            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.GetPublishedBlogBySlug", con)
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
                    success = false,
                    message = "The requested published blog could not be found."
                });
            }

            return Ok(ReadBlog(reader));
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to retrieve the requested blog. Please try again later."
            });
        }
    }

    // ==========================================
    // 3. ADMIN GET ALL BLOGS
    // GET /api/Blog/admin/all
    // ==========================================

    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllForAdmin(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        var denied = RequireAdminKey(adminKey);
        if (denied != null) return denied;

        try
        {
            var blogs = new List<BlogModel>();

            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.GetAllBlogs", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                blogs.Add(ReadBlog(reader));

            return Ok(blogs);
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to retrieve the blog management list. Please try again later."
            });
        }
    }

    // ==========================================
    // 4. ADMIN POST - CREATE BLOG
    // POST /api/Blog
    // ------------------------------------------
    // UPDATED: Multiple files via Images list
    //          + external URLs via ImageUrl field
    //          All combined → comma-separated ImageUrl saved to DB
    // ==========================================

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromForm] BlogModel model)
    {
        var denied = RequireAdminKey(adminKey);
        if (denied != null) return denied;

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Slug) ||
            string.IsNullOrWhiteSpace(model.Content))
        {
            return BadRequest(new
            {
                success = false,
                message = "Title, slug, and content are required fields."
            });
        }

        var allUrls = new List<string>();

        try
        {
            // ── UPLOAD MULTIPLE FILES TO CLOUDINARY ───────
            if (model.Image != null && model.Image.Count > 0)
            {
                foreach (var file in model.Image)
                {
                    var fileError = ValidateMediaFile(file);
                    if (fileError != null)
                        return BadRequest(new { success = false, message = fileError });

                    var uploadedUrl = await SaveMediaFileAsync(file);
                    if (!string.IsNullOrWhiteSpace(uploadedUrl))
                        allUrls.Add(uploadedUrl);
                }
            }
            // ─────────────────────────────────────────────

            // ── ADD EXTERNAL URLs FROM FRONTEND ───────────
            if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                var externalUrls = model.ImageUrl
                    .Split(',', StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries)
                    .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _));

                allUrls.AddRange(externalUrls);
            }
            // ─────────────────────────────────────────────

            // ── COMBINE → comma-separated string ──────────
            model.ImageUrl = allUrls.Count > 0
                ? string.Join(",", allUrls)
                : null;
            // ─────────────────────────────────────────────

            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.CreateBlog", con)
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
                    success = false,
                    message = "The blog could not be created. Please try again."
                });
            }

            var saved = ReadBlog(reader);

            return CreatedAtAction(
                nameof(GetBySlug),
                new { slug = saved.Slug },
                new
                {
                    success = true,
                    message = "Blog created successfully.",
                    blog = saved
                }
            );
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                success = false,
                message = "This blog slug already exists. Please use a unique slug."
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to save the blog due to a database error. Please try again later."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while creating the blog. Please verify the upload configuration and try again."
            });
        }
    }

    // ==========================================
    // 5. ADMIN PUT - UPDATE BLOG
    // PUT /api/Blog/{id}
    // ------------------------------------------
    // UPDATED: Multiple files via Images list
    //          Frontend sends kept URLs in ImageUrl
    //          New uploads + kept URLs combined
    // ==========================================

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey,
        [FromForm] BlogModel model)
    {
        var denied = RequireAdminKey(adminKey);
        if (denied != null) return denied;

        if (id <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Please provide a valid blog ID."
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
                success = false,
                message = "Title, slug, and content are required fields."
            });
        }

        model.BlogId = id;

        var allUrls = new List<string>();

        try
        {
            // ── UPLOAD NEW FILES TO CLOUDINARY ────────────
            if (model.Image != null && model.Image.Count > 0)
            {
                foreach (var file in model.Image)
                {
                    var fileError = ValidateMediaFile(file);
                    if (fileError != null)
                        return BadRequest(new { success = false, message = fileError });

                    var uploadedUrl = await SaveMediaFileAsync(file);
                    if (!string.IsNullOrWhiteSpace(uploadedUrl))
                        allUrls.Add(uploadedUrl);
                }
            }
            // ─────────────────────────────────────────────

            // ── ADD KEPT EXISTING + MANUAL URLs ───────────
            // Frontend sends back only URLs the user kept
            // (removed ones are already excluded on frontend)
            if (!string.IsNullOrWhiteSpace(model.ImageUrl))
            {
                var externalUrls = model.ImageUrl
                    .Split(',', StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries)
                    .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _));

                allUrls.AddRange(externalUrls);
            }
            // ─────────────────────────────────────────────

            // ── COMBINE or PRESERVE OLD ───────────────────
            if (allUrls.Count > 0)
            {
                model.ImageUrl = string.Join(",", allUrls);
            }
            else
            {
                // Nothing uploaded, nothing kept → preserve old DB value
                model.ImageUrl = await GetExistingImageUrlAsync(id);
            }
            // ─────────────────────────────────────────────

            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.UpdateBlog", con)
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
                    success = false,
                    message = "The blog you are trying to update could not be found."
                });
            }

            var updated = ReadBlog(reader);

            return Ok(new
            {
                success = true,
                message = "Blog updated successfully.",
                blog = updated
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                success = false,
                message = "This blog slug is already in use. Please choose a different slug."
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to update the blog due to a database error. Please try again later."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while updating the blog. Please verify the upload configuration and try again."
            });
        }
    }

    // ==========================================
    // 6. ADMIN PATCH - CHANGE BLOG STATUS
    // PATCH /api/Blog/{id}/status?isPublished=true
    // ==========================================

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateBlogStatus(
        int id,
        [FromQuery] bool isPublished,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        var denied = RequireAdminKey(adminKey);
        if (denied != null) return denied;

        if (id <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Please provide a valid blog ID."
            });
        }

        try
        {
            var blog = await GetBlogByIdAsync(id);

            if (blog == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "The requested blog could not be found."
                });
            }

            blog.IsPublished = isPublished;

            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.UpdateBlog", con)
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
                    success = false,
                    message = "The blog status could not be updated because the blog was not found."
                });
            }

            return Ok(new
            {
                success = true,
                message = isPublished
                    ? "Blog published successfully."
                    : "Blog moved to draft successfully.",
                blog = ReadBlog(reader)
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to update the blog status due to a database error."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "An unexpected error occurred while changing the blog status."
            });
        }
    }

    // ==========================================
    // 7. ADMIN DELETE - DELETE BLOG
    // DELETE /api/Blog/{id}
    // ==========================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        var denied = RequireAdminKey(adminKey);
        if (denied != null) return denied;

        if (id <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Please provide a valid blog ID."
            });
        }

        try
        {
            var oldImageUrl = await GetExistingImageUrlAsync(id);

            await using var con = CreateConnection();
            await using var cmd = new SqlCommand("dbo.DeleteBlog", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@BlogId", SqlDbType.Int).Value = id;

            await con.OpenAsync();

            var affected = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            if (affected == 0)
            {
                return NotFound(new
                {
                    success = false,
                    message = "The blog you are trying to delete could not be found."
                });
            }

            DeleteBlogImage(oldImageUrl);

            return Ok(new
            {
                success = true,
                message = "Blog deleted successfully.",
                blogId = id
            });
        }
        catch (SqlException)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to delete the blog due to a database error. Please try again later."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "An unexpected error occurred while deleting the blog."
            });
        }
    }

    // ==========================================
    // 8. MEDIA PROXY - CORS BYPASS FOR PREVIEW
    // GET /api/Blog/proxy?url=https://...
    // ------------------------------------------
    // Frontend se CORS-blocked URLs ko backend
    // server-side fetch karke browser ko return
    // karta hai. Preview ke liye use hota hai.
    // ==========================================

    [HttpGet("proxy")]
    public async Task<IActionResult> ProxyMedia([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new
            {
                success = false,
                message = "Please provide a valid media URL."
            });
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new
            {
                success = false,
                message = "Only absolute HTTP/HTTPS URLs are allowed."
            });
        }

        try
        {
            using var http = new HttpClient();

            // Browser-like User-Agent so servers don't block the request
            http.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/120.0.0.0 Safari/537.36"
            );

            http.Timeout = TimeSpan.FromSeconds(15);

            var response = await http.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new
                {
                    success = false,
                    message = "The remote server returned an error while fetching the media."
                });
            }

            var contentType = response.Content.Headers.ContentType?.ToString()
                ?? "application/octet-stream";

            var bytes = await response.Content.ReadAsByteArrayAsync();

            // Browser mein 1 ghante ke liye cache karo
            Response.Headers["Cache-Control"] = "public, max-age=3600";

            return File(bytes, contentType);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new
            {
                success = false,
                message = "The request to the remote media URL timed out."
            });
        }
        catch (Exception)
        {
            return StatusCode(502, new
            {
                success = false,
                message = "Unable to fetch the media from the provided URL."
            });
        }
    }
}


//---------------------------------------------------------------new CoDE END----------------------------------------------------------------------
