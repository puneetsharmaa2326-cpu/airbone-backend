
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MediaMasterApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    // =========================================
    // MEDIA UPLOAD REQUEST DTO
    // =========================================

    public class MediaUploadRequest
    {
        [Required]
        public string MediaName { get; set; } = "";

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public string MediaType { get; set; } = "";

        public bool IsActive { get; set; } = true;

        [Required]
        public IFormFile File { get; set; } = null!;
    }

    // =========================================
    // MEDIA CONTROLLER
    // =========================================

    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly Cloudinary? _cloudinary;

        public MediaController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Database connection string missing.");

            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (!string.IsNullOrWhiteSpace(cloudName) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(apiSecret))
            {
                _cloudinary = new Cloudinary(
                    new Account(cloudName, apiKey, apiSecret));

                _cloudinary.Api.Secure = true;
            }
        }

        // =========================================
        // 1. COMMON SQL PARAMETERS
        // =========================================

        private static void AddParameters(
            SqlCommand cmd,
            string action,
            int? id = null,
            string? name = null,
            string? type = null,
            string? url = null,
            string? publicId = null,
            bool? isActive = null,
            int? categoryId = null)
        {
            cmd.Parameters.Add("@Action", SqlDbType.NVarChar, 20)
                .Value = action;

            cmd.Parameters.Add("@MediaId", SqlDbType.Int)
                .Value = (object?)id ?? DBNull.Value;

            cmd.Parameters.Add("@MediaName", SqlDbType.NVarChar, 200)
                .Value = (object?)name ?? DBNull.Value;

            cmd.Parameters.Add("@MediaType", SqlDbType.NVarChar, 20)
                .Value = (object?)type ?? DBNull.Value;

            cmd.Parameters.Add("@MediaUrl", SqlDbType.NVarChar, 1000)
                .Value = (object?)url ?? DBNull.Value;

            cmd.Parameters.Add("@PublicId", SqlDbType.NVarChar, 300)
                .Value = (object?)publicId ?? DBNull.Value;

            cmd.Parameters.Add("@IsActive", SqlDbType.Bit)
                .Value = (object?)isActive ?? DBNull.Value;

            // CategoryId parameter added
            cmd.Parameters.Add("@CategoryId", SqlDbType.Int)
                .Value = (object?)categoryId ?? DBNull.Value;
        }

        // =========================================
        // 2. READ MEDIA DATA
        // =========================================

        private static MediaModel ReadMedia(SqlDataReader reader)
        {
            return new MediaModel
            {
                MediaId = Convert.ToInt32(reader["MediaId"]),

                MediaName = Convert.ToString(
                    reader["MediaName"]) ?? "",

                // Handle NULL CategoryId for old records
                CategoryId = reader["CategoryId"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["CategoryId"]),

                // Handle NULL CategoryName
                CategoryName = reader["CategoryName"] == DBNull.Value
                    ? ""
                    : Convert.ToString(reader["CategoryName"]) ?? "",

                MediaType = Convert.ToString(
                    reader["MediaType"]) ?? "",

                MediaUrl = Convert.ToString(
                    reader["MediaUrl"]) ?? "",

                PublicId = Convert.ToString(
                    reader["PublicId"]) ?? "",

                IsActive = Convert.ToBoolean(
                    reader["IsActive"]),

                CreatedDate = Convert.ToDateTime(
                    reader["CreatedDate"])
            };
        }

        // =========================================
        // 3. GET MEDIA LIST FROM DATABASE
        // =========================================

        private async Task<List<MediaModel>> GetList(
            string action,
            int? id = null)
        {
            var list = new List<MediaModel>();

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand("dbo.ManageMedia", con);

            cmd.CommandType = CommandType.StoredProcedure;

            AddParameters(cmd, action, id);

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(ReadMedia(reader));
            }

            return list;
        }

        // =========================================
        // 4. VALIDATE CATEGORY
        // =========================================

        private async Task<bool> IsValidCategory(int categoryId)
        {
            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.CategoryMaster
                  WHERE CategoryId = @CategoryId
                  AND IsActive = 1",
                con);

            cmd.Parameters.Add(
                "@CategoryId", SqlDbType.Int).Value = categoryId;

            await con.OpenAsync();

            int count = Convert.ToInt32(
                await cmd.ExecuteScalarAsync());

            return count > 0;
        }

        // =========================================
        // 5. GET ALL MEDIA
        // GET: api/Media
        // =========================================

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var data = await GetList("GETALL");

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to fetch media.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 6. GET ACTIVE MEDIA
        // GET: api/Media/active
        // =========================================

        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var data = await GetList("GETACTIVE");

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to fetch active media.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 7. GET MEDIA BY ID
        // GET: api/Media/5
        // =========================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid MediaId.");

            try
            {
                var data = await GetList("GETBYID", id);

                if (data.Count == 0)
                    return NotFound("Media not found.");

                return Ok(data[0]);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to fetch media.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 8. UPLOAD MEDIA WITH CATEGORY
        // POST: api/Media/upload
        // =========================================

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Upload(
            [FromForm] MediaUploadRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Upload data is required.");

                string mediaName = request.MediaName;
                int categoryId = request.CategoryId;
                string mediaType = request.MediaType;
                bool isActive = request.IsActive;
                IFormFile file = request.File;

                // Check Cloudinary configuration

                if (_cloudinary == null)
                {
                    return StatusCode(503,
                        "Configure Cloudinary credentials before uploading media.");
                }

                // Validate media name

                if (string.IsNullOrWhiteSpace(mediaName) ||
                    mediaName.Length > 200)
                {
                    return BadRequest(
                        "Media name required (max 200 characters).");
                }

                // Validate category

                if (categoryId <= 0)
                {
                    return BadRequest(
                        "Please select a valid category.");
                }

                bool categoryExists =
                    await IsValidCategory(categoryId);

                if (!categoryExists)
                {
                    return BadRequest(
                        "Selected category does not exist or is inactive.");
                }

                // Validate file

                if (file == null || file.Length == 0)
                {
                    return BadRequest(
                        "Select an image or video.");
                }

                if (file.Length > 50 * 1024 * 1024)
                {
                    return BadRequest(
                        "Maximum file size is 50 MB.");
                }

                mediaType = mediaType?.Trim() ?? "";

                string extension = Path
                    .GetExtension(file.FileName)
                    .ToLowerInvariant();

                string[] imageExtensions =
                {
                    ".jpg", ".jpeg", ".jfif",
                    ".png", ".webp", ".gif"
                };

                string[] videoExtensions =
                {
                    ".mp4", ".webm", ".mov"
                };

                bool isImage =
                    mediaType.Equals(
                        "Image",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    file.ContentType.StartsWith(
                        "image/",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    imageExtensions.Contains(extension);

                bool isVideo =
                    mediaType.Equals(
                        "Video",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    file.ContentType.StartsWith(
                        "video/",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    videoExtensions.Contains(extension);

                if (!isImage && !isVideo)
                {
                    return BadRequest(
                        "Unsupported image/video format.");
                }

                // =================================
                // Upload to Cloudinary
                // =================================

                string mediaUrl;
                string publicId;

                await using var stream = file.OpenReadStream();

                if (isImage)
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            file.FileName, stream),

                        Folder = "mediamaster"
                    };

                    var result = await _cloudinary.UploadAsync(
                        uploadParams);

                    if (result.Error != null ||
                        result.SecureUrl == null)
                    {
                        return StatusCode(
                            502,
                            "Cloudinary image upload failed.");
                    }

                    mediaUrl = result.SecureUrl.ToString();
                    publicId = result.PublicId;
                }
                else
                {
                    var uploadParams = new VideoUploadParams
                    {
                        File = new FileDescription(
                            file.FileName, stream),

                        Folder = "mediamaster"
                    };

                    var result = await _cloudinary.UploadAsync(
                        uploadParams);

                    if (result.Error != null ||
                        result.SecureUrl == null)
                    {
                        return StatusCode(
                            502,
                            "Cloudinary video upload failed.");
                    }

                    mediaUrl = result.SecureUrl.ToString();
                    publicId = result.PublicId;
                }

                // =================================
                // Save media in SQL Server
                // =================================

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand("dbo.ManageMedia", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "INSERT",
                    name: mediaName.Trim(),
                    type: isImage ? "Image" : "Video",
                    url: mediaUrl,
                    publicId: publicId,
                    isActive: isActive,
                    categoryId: categoryId
                );

                await con.OpenAsync();

                await using var reader =
                    await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException(
                        "No saved record returned from database.");
                }

                var saved = ReadMedia(reader);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = saved.MediaId },
                    new
                    {
                        success = true,
                        message = "Media uploaded and saved successfully.",
                        data = saved
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Media upload or database save failed.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 9. UPDATE MEDIA STATUS
        // PATCH: api/Media/5/status
        // =========================================

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] MediaStatusRequest request)
        {
            if (id <= 0)
                return BadRequest("Invalid MediaId.");

            if (request == null)
                return BadRequest("Status request is required.");

            try
            {
                var existing = await GetList("GETBYID", id);

                if (existing.Count == 0)
                    return NotFound("Media not found.");

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand("dbo.ManageMedia", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "SETSTATUS",
                    id: id,
                    isActive: request.IsActive
                );

                await con.OpenAsync();

                await using var reader =
                    await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return NotFound(
                        "Media status update failed.");
                }

                return Ok(new
                {
                    success = true,
                    message = "Media status updated successfully.",
                    data = ReadMedia(reader)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to update media status.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 10. DELETE MEDIA
        // DELETE: api/Media/5
        // =========================================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid MediaId.");

            try
            {
                var data = await GetList("GETBYID", id);

                if (data.Count == 0)
                    return NotFound("Media not found.");

                var item = data[0];

                if (_cloudinary == null)
                {
                    return StatusCode(
                        503,
                        "Configure Cloudinary credentials before deleting media.");
                }

                // Delete from Cloudinary

                var resourceType = item.MediaType == "Video"
                    ? CloudinaryDotNet.Actions.ResourceType.Video
                    : CloudinaryDotNet.Actions.ResourceType.Image;

                var deleteParams = new DeletionParams(
                    item.PublicId)
                {
                    ResourceType = resourceType
                };

                var result = await _cloudinary.DestroyAsync(
                    deleteParams);

                if (result.Error != null)
                {
                    return StatusCode(
                        502,
                        "Cloudinary delete failed.");
                }

                // Delete from SQL Server

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand("dbo.ManageMedia", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "DELETE",
                    id: id
                );

                await con.OpenAsync();

                int rows = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync());

                if (rows == 0)
                {
                    return NotFound(
                        "Database record not found.");
                }

                return Ok(new
                {
                    success = true,
                    message = "Media deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to delete media.",
                    error = ex.Message
                });
            }
        }
    }
}
