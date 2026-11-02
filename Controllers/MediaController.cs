
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MediaMasterApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MediaMasterApi.Controllers
{
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

        // Common SQL parameters
        private static void AddParameters(
            SqlCommand cmd,
            string action,
            int? id = null,
            string? name = null,
            string? type = null,
            string? url = null,
            string? publicId = null,
            bool? isActive = null)
        {
            cmd.Parameters.Add("@Action", SqlDbType.NVarChar, 20)
                .Value = action;

            cmd.Parameters.Add("@MediaId", SqlDbType.Int)
                .Value = id.HasValue ? id.Value : DBNull.Value;

            cmd.Parameters.Add("@MediaName", SqlDbType.NVarChar, 200)
                .Value = name == null ? DBNull.Value : name;

            cmd.Parameters.Add("@MediaType", SqlDbType.NVarChar, 20)
                .Value = type == null ? DBNull.Value : type;

            cmd.Parameters.Add("@MediaUrl", SqlDbType.NVarChar, 1000)
                .Value = url == null ? DBNull.Value : url;

            cmd.Parameters.Add("@PublicId", SqlDbType.NVarChar, 300)
                .Value = publicId == null ? DBNull.Value : publicId;

            cmd.Parameters.Add("@IsActive", SqlDbType.Bit)
                .Value = isActive.HasValue
                    ? isActive.Value
                    : DBNull.Value;
        }

        // SQL reader ko model mein convert karna
        private static MediaModel ReadMedia(SqlDataReader reader)
        {
            return new MediaModel
            {
                MediaId = Convert.ToInt32(reader["MediaId"]),
                MediaName = Convert.ToString(reader["MediaName"])!,
                MediaType = Convert.ToString(reader["MediaType"])!,
                MediaUrl = Convert.ToString(reader["MediaUrl"])!,
                PublicId = Convert.ToString(reader["PublicId"])!,
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
            };
        }

        // Database se records lana
        private async Task<List<MediaModel>> GetList(string action, int? id = null)
        {
            var list = new List<MediaModel>();

            await using var con = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("dbo.ManageMedia", con);

            cmd.CommandType = CommandType.StoredProcedure;
            AddParameters(cmd, action, id);

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                list.Add(ReadMedia(reader));

            return list;
        }

        // GET: api/Media - Admin list, all records
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await GetList("GETALL"));
        }

        // GET: api/Media/active - Public gallery
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            return Ok(await GetList("GETACTIVE"));
        }

        // GET: api/Media/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid MediaId.");

            var data = await GetList("GETBYID", id);

            if (data.Count == 0)
                return NotFound("Media not found.");

            return Ok(data[0]);
        }

        // POST: api/Media/upload
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> Upload(
            [FromForm] string mediaName,
            [FromForm] string mediaType,
            IFormFile file)
        {
            if (_cloudinary == null)
                return StatusCode(503, "Configure Cloudinary credentials before uploading media.");

            if (string.IsNullOrWhiteSpace(mediaName) ||
                mediaName.Length > 200)
                return BadRequest("Media name required (max 200 characters).");

            if (file == null || file.Length == 0)
                return BadRequest("Select an image or video.");

            if (file.Length > 50 * 1024 * 1024)
                return BadRequest("Maximum file size is 50 MB.");

            mediaType = mediaType?.Trim() ?? "";

            string extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();
            string[] imageExtensions =
                { ".jpg", ".jpeg", ".jfif", ".png", ".webp", ".gif" };

            string[] videoExtensions =
                { ".mp4", ".webm", ".mov" };

            bool isImage =
                mediaType == "Image" &&
                file.ContentType.StartsWith(
                    "image/", StringComparison.OrdinalIgnoreCase) &&
                imageExtensions.Contains(extension);

            bool isVideo =
                mediaType == "Video" &&
                file.ContentType.StartsWith(
                    "video/", StringComparison.OrdinalIgnoreCase) &&
                videoExtensions.Contains(extension);

            if (!isImage && !isVideo)
                return BadRequest("Unsupported image/video format.");

            string mediaUrl;
            string publicId;

            try
            {
                await using var stream = file.OpenReadStream();

                if (isImage)
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "mediamaster"
                    };

                    var result = await _cloudinary.UploadAsync(uploadParams);

                    if (result.Error != null || result.SecureUrl == null)
                        return StatusCode(502, "Cloudinary image upload failed.");

                    mediaUrl = result.SecureUrl.ToString();
                    publicId = result.PublicId;
                }
                else
                {
                    var uploadParams = new VideoUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "mediamaster"
                    };

                    var result = await _cloudinary.UploadAsync(uploadParams);

                    if (result.Error != null || result.SecureUrl == null)
                        return StatusCode(502, "Cloudinary video upload failed.");

                    mediaUrl = result.SecureUrl.ToString();
                    publicId = result.PublicId;
                }
            }
            catch
            {
                return StatusCode(502, "Cloudinary upload failed.");
            }

            try
            {
                await using var con = new SqlConnection(_connectionString);
                await using var cmd = new SqlCommand("dbo.ManageMedia", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(
                    cmd, "INSERT",
                    name: mediaName.Trim(),
                    type: mediaType,
                    url: mediaUrl,
                    publicId: publicId,
                    isActive: true);

                await con.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("No saved record returned.");

                var saved = ReadMedia(reader);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = saved.MediaId },
                    saved);
            }
            catch
            {
                // Cloudinary par upload ho chuki hai;
                // production mein orphaned asset cleanup/retry rakhein.
                return StatusCode(500,
                    "File Cloudinary par upload hui, lekin SQL save fail hua.");
            }
        }

        // PATCH: api/Media/5/status
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id, [FromBody] MediaStatusRequest request)
        {
            if (id <= 0)
                return BadRequest("Invalid MediaId.");

            var existing = await GetList("GETBYID", id);

            if (existing.Count == 0)
                return NotFound("Media not found.");

            await using var con = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("dbo.ManageMedia", con);

            cmd.CommandType = CommandType.StoredProcedure;

            AddParameters(
                cmd, "SETSTATUS",
                id: id,
                isActive: request.IsActive);

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound("Status update failed.");

            return Ok(ReadMedia(reader));
        }

        // DELETE: api/Media/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid MediaId.");

            var data = await GetList("GETBYID", id);

            if (data.Count == 0)
                return NotFound("Media not found.");

            var item = data[0];

            if (_cloudinary == null)
                return StatusCode(503, "Configure Cloudinary credentials before deleting media.");

            // Cloudinary se asset delete karein
            try
            {
                var resourceType = item.MediaType == "Video"
                    ? CloudinaryDotNet.Actions.ResourceType.Video
                    : CloudinaryDotNet.Actions.ResourceType.Image;

                var deleteParams = new DeletionParams(item.PublicId)
                {
                    ResourceType = resourceType
                };

                var result = await _cloudinary.DestroyAsync(deleteParams);

                if (result.Error != null)
                    return StatusCode(502, "Cloudinary delete failed.");
            }
            catch
            {
                return StatusCode(502, "Cloudinary delete failed.");
            }

            await using var con = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("dbo.ManageMedia", con);

            cmd.CommandType = CommandType.StoredProcedure;
            AddParameters(cmd, "DELETE", id);

            await con.OpenAsync();
            var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            if (rows == 0)
                return NotFound("Database record not found.");

            return Ok(new { message = "Media deleted successfully." });
        }
    }
}
