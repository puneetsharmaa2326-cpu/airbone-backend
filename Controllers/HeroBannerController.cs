
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HeroBannerController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly Cloudinary? _cloudinary;

        public HeroBannerController(IConfiguration configuration)
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
            string? title = null,
            string? type = null,
            string? url = null,
            string? publicId = null,
            bool? isActive = null)
        {
            cmd.Parameters.Add("@Action", SqlDbType.NVarChar, 20)
                .Value = action;

            cmd.Parameters.Add("@BannerId", SqlDbType.Int)
                .Value = (object?)id ?? DBNull.Value;

            cmd.Parameters.Add("@BannerTitle", SqlDbType.NVarChar, 200)
                .Value = (object?)title ?? DBNull.Value;

            cmd.Parameters.Add("@MediaType", SqlDbType.NVarChar, 20)
                .Value = (object?)type ?? DBNull.Value;

            cmd.Parameters.Add("@MediaUrl", SqlDbType.NVarChar, 1000)
                .Value = (object?)url ?? DBNull.Value;

            cmd.Parameters.Add("@PublicId", SqlDbType.NVarChar, 300)
                .Value = (object?)publicId ?? DBNull.Value;

            cmd.Parameters.Add("@IsActive", SqlDbType.Bit)
                .Value = (object?)isActive ?? DBNull.Value;
        }

        // =========================================
        // 2. READ DATABASE RECORD
        // =========================================

        private static HeroBannerModel ReadBanner(
            SqlDataReader reader)
        {
            return new HeroBannerModel
            {
                BannerId = Convert.ToInt32(
                    reader["BannerId"]),

                BannerTitle = Convert.ToString(
                    reader["BannerTitle"]) ?? "",

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
        // 3. GET MEDIA LIST
        // =========================================

        private async Task<List<HeroBannerModel>> GetList(
            string action,
            int? id = null)
        {
            var list = new List<HeroBannerModel>();

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand("dbo.ManageHeroBanner", con);

            cmd.CommandType = CommandType.StoredProcedure;

            AddParameters(cmd, action, id);

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(ReadBanner(reader));
            }

            return list;
        }

        // =========================================
        // 4. VALIDATE IMAGE / VIDEO
        // =========================================

        private IActionResult? ValidateFile(
            IFormFile? file,
            string mediaType)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(
                    "Please select an image or video.");
            }

            if (file.Length > 50 * 1024 * 1024)
            {
                return BadRequest(
                    "Maximum file size is 50 MB.");
            }

            string extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            string[] imageExtensions =
            {
                ".jpg", ".jpeg", ".png",
                ".webp", ".gif"
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
                imageExtensions.Contains(extension)
                &&
                file.ContentType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase);

            bool isVideo =
                mediaType.Equals(
                    "Video",
                    StringComparison.OrdinalIgnoreCase)
                &&
                videoExtensions.Contains(extension)
                &&
                file.ContentType.StartsWith(
                    "video/",
                    StringComparison.OrdinalIgnoreCase);

            if (!isImage && !isVideo)
            {
                return BadRequest(
                    "Invalid media type or file format. " +
                    "Select Image with an image file or Video with a video file.");
            }

            return null;
        }

        // =========================================
        // 5. CLOUDINARY UPLOAD
        // =========================================

        private async Task<(string Url, string PublicId)>
            UploadToCloudinary(
                IFormFile file,
                string mediaType)
        {
            if (_cloudinary == null)
            {
                throw new InvalidOperationException(
                    "Cloudinary configuration missing.");
            }

            await using var stream = file.OpenReadStream();

            if (mediaType.Equals(
                "Image",
                StringComparison.OrdinalIgnoreCase))
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(
                        file.FileName, stream),

                    Folder = "herobanners"
                };

                var result = await _cloudinary.UploadAsync(
                    uploadParams);

                if (result.Error != null ||
                    result.SecureUrl == null)
                {
                    throw new Exception(
                        "Cloudinary image upload failed.");
                }

                return (
                    result.SecureUrl.ToString(),
                    result.PublicId
                );
            }
            else
            {
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(
                        file.FileName, stream),

                    Folder = "herobanners"
                };

                var result = await _cloudinary.UploadAsync(
                    uploadParams);

                if (result.Error != null ||
                    result.SecureUrl == null)
                {
                    throw new Exception(
                        "Cloudinary video upload failed.");
                }

                return (
                    result.SecureUrl.ToString(),
                    result.PublicId
                );
            }
        }

        // =========================================
        // 6. CLOUDINARY DELETE
        // =========================================

        private async Task DeleteFromCloudinary(
            string publicId,
            string mediaType)
        {
            if (_cloudinary == null)
            {
                throw new InvalidOperationException(
                    "Cloudinary configuration missing.");
            }

            var resourceType = mediaType.Equals(
                "Video",
                StringComparison.OrdinalIgnoreCase)
                ? ResourceType.Video
                : ResourceType.Image;

            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = resourceType
            };

            var result = await _cloudinary.DestroyAsync(
                deleteParams);

            if (result.Error != null)
            {
                throw new Exception(
                    "Cloudinary media deletion failed.");
            }
        }

        // =========================================
        // 7. GET ALL HERO BANNERS
        // GET: api/HeroBanner
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
                    message = "Unable to fetch hero banners.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 8. GET ACTIVE HERO BANNERS
        // GET: api/HeroBanner/active
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
                    message = "Unable to fetch active banners.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 9. GET BANNER BY ID
        // GET: api/HeroBanner/5
        // =========================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid BannerId.");

            try
            {
                var data = await GetList("GETBYID", id);

                if (data.Count == 0)
                    return NotFound("Hero banner not found.");

                return Ok(data[0]);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to fetch hero banner.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 10. UPLOAD HERO BANNER
        // POST: api/HeroBanner/upload
        // =========================================

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Upload(
            [FromForm] HeroBannerUploadRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(
                    request.BannerTitle) ||
                    request.BannerTitle.Length > 200)
                {
                    return BadRequest(
                        "Banner title is required (max 200 characters).");
                }

                string mediaType =
                    request.MediaType?.Trim() ?? "";

                if (!mediaType.Equals(
                        "Image",
                        StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals(
                        "Video",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(
                        "MediaType must be Image or Video.");
                }

                if (_cloudinary == null)
                {
                    return StatusCode(
                        503,
                        "Cloudinary is not configured.");
                }

                var validation = ValidateFile(
                    request.File, mediaType);

                if (validation != null)
                    return validation;

                var uploaded = await UploadToCloudinary(
                    request.File, mediaType);

                try
                {
                    await using var con =
                        new SqlConnection(_connectionString);

                    await using var cmd =
                        new SqlCommand(
                            "dbo.ManageHeroBanner", con);

                    cmd.CommandType =
                        CommandType.StoredProcedure;

                    AddParameters(
                        cmd,
                        "INSERT",
                        title: request.BannerTitle.Trim(),
                        type: mediaType,
                        url: uploaded.Url,
                        publicId: uploaded.PublicId,
                        isActive: request.IsActive
                    );

                    await con.OpenAsync();

                    await using var reader =
                        await cmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        throw new Exception(
                            "Banner was not saved in database.");
                    }

                    var saved = ReadBanner(reader);

                    return CreatedAtAction(
                        nameof(GetById),
                        new { id = saved.BannerId },
                        new
                        {
                            success = true,
                            message = "Hero banner uploaded successfully.",
                            data = saved
                        });
                }
                catch
                {
                    // Remove uploaded file if SQL save fails
                    await DeleteFromCloudinary(
                        uploaded.PublicId, mediaType);

                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Hero banner upload failed.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 11. UPDATE HERO BANNER
        // PUT: api/HeroBanner/5
        // =========================================

        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Update(
            int id,
            [FromForm] HeroBannerUploadRequest request)
        {
            string? newPublicId = null;
            string? newMediaType = null;

            try
            {
                if (id <= 0)
                    return BadRequest("Invalid BannerId.");

                if (string.IsNullOrWhiteSpace(
                    request.BannerTitle) ||
                    request.BannerTitle.Length > 200)
                {
                    return BadRequest(
                        "Banner title is required.");
                }

                string mediaType =
                    request.MediaType?.Trim() ?? "";

                if (!mediaType.Equals(
                        "Image",
                        StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals(
                        "Video",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(
                        "MediaType must be Image or Video.");
                }

                if (_cloudinary == null)
                {
                    return StatusCode(
                        503,
                        "Cloudinary is not configured.");
                }

                var existing = await GetList(
                    "GETBYID", id);

                if (existing.Count == 0)
                    return NotFound("Hero banner not found.");

                var validation = ValidateFile(
                    request.File, mediaType);

                if (validation != null)
                    return validation;

                var oldBanner = existing[0];

                // Upload new media first
                var uploaded = await UploadToCloudinary(
                    request.File, mediaType);

                newPublicId = uploaded.PublicId;
                newMediaType = mediaType;

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand(
                        "dbo.ManageHeroBanner", con);

                cmd.CommandType =
                    CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "UPDATE",
                    id: id,
                    title: request.BannerTitle.Trim(),
                    type: mediaType,
                    url: uploaded.Url,
                    publicId: uploaded.PublicId,
                    isActive: request.IsActive
                );

                await con.OpenAsync();

                await using var reader =
                    await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    throw new Exception("Banner update failed.");

                var updated = ReadBanner(reader);

                // New record saved successfully
                newPublicId = null;

                // Delete old media after successful update
                await DeleteFromCloudinary(
                    oldBanner.PublicId,
                    oldBanner.MediaType);

                return Ok(new
                {
                    success = true,
                    message = "Hero banner updated successfully.",
                    data = updated
                });
            }
            catch (Exception ex)
            {
                // Cleanup new upload if update failed
                if (!string.IsNullOrWhiteSpace(newPublicId))
                {
                    try
                    {
                        await DeleteFromCloudinary(
                            newPublicId,
                            newMediaType ?? "Image");
                    }
                    catch
                    {
                        // Keep original error
                    }
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Hero banner update failed.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 12. UPDATE BANNER STATUS
        // PATCH: api/HeroBanner/5/status
        // =========================================

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] HeroBannerStatusRequest request)
        {
            if (id <= 0)
                return BadRequest("Invalid BannerId.");

            try
            {
                var existing = await GetList(
                    "GETBYID", id);

                if (existing.Count == 0)
                    return NotFound("Hero banner not found.");

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand(
                        "dbo.ManageHeroBanner", con);

                cmd.CommandType =
                    CommandType.StoredProcedure;

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
                    return NotFound("Status update failed.");

                return Ok(new
                {
                    success = true,
                    message = "Hero banner status updated successfully.",
                    data = ReadBanner(reader)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to update banner status.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 13. DELETE HERO BANNER
        // DELETE: api/HeroBanner/5
        // =========================================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid BannerId.");

            try
            {
                var existing = await GetList(
                    "GETBYID", id);

                if (existing.Count == 0)
                    return NotFound("Hero banner not found.");

                if (_cloudinary == null)
                {
                    return StatusCode(
                        503,
                        "Cloudinary is not configured.");
                }

                var item = existing[0];

                // Delete database record first
                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand(
                        "dbo.ManageHeroBanner", con);

                cmd.CommandType =
                    CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "DELETE",
                    id: id
                );

                await con.OpenAsync();

                int rows = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync());

                if (rows == 0)
                    return NotFound("Database record not found.");

                // Delete media from Cloudinary
                await DeleteFromCloudinary(
                    item.PublicId,
                    item.MediaType);

                return Ok(new
                {
                    success = true,
                    message = "Hero banner deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to delete hero banner.",
                    error = ex.Message
                });
            }
        }
    }
}
