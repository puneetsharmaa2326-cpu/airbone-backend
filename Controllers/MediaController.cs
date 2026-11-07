
using System.Data;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly string _connectionString;
    private readonly Cloudinary _cloudinary;

    private const long MaxFileSize = 50 * 1024 * 1024;

    private static readonly string[] ImageExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".webm", ".mov"
    };

    public MediaController(
        IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new Exception("Database connection missing.");

        var cloud = configuration.GetSection("Cloudinary");

        var account = new Account(
            cloud["CloudName"],
            cloud["ApiKey"],
            cloud["ApiSecret"]
        );

        _cloudinary = new Cloudinary(account);
    }

    // =====================================================
    // DATABASE CONNECTION
    // =====================================================

    private SqlConnection GetConnection()
    {
        return new SqlConnection(_connectionString);
    }

    private static void AddParameter(
        SqlCommand cmd,
        string name,
        object? value,
        SqlDbType? type = null,
        int? size = null)
    {
        SqlParameter parameter;

        if (type.HasValue)
        {
            parameter = size.HasValue
                ? cmd.Parameters.Add(name, type.Value, size.Value)
                : cmd.Parameters.Add(name, type.Value);
        }
        else
        {
            parameter = cmd.Parameters.Add(
                name,
                SqlDbType.Variant
            );
        }

        parameter.Value = value ?? DBNull.Value;
    }

    // =====================================================
    // READ MEDIA FILE
    // =====================================================

    private static MediaFileModel ReadMediaFile(
        SqlDataReader reader)
    {
        return new MediaFileModel
        {
            MediaFileId = Convert.ToInt32(
                reader["MediaFileId"]),

            MediaId = Convert.ToInt32(
                reader["MediaId"]),

            MediaType = reader["MediaType"]?.ToString() ?? "",

            MediaUrl = reader["MediaUrl"]?.ToString() ?? "",

            PublicId = reader["PublicId"] == DBNull.Value
                ? null
                : reader["PublicId"].ToString(),

            CreatedDate = Convert.ToDateTime(
                reader["CreatedDate"])
        };
    }

    // =====================================================
    // GET FILES BY MEDIA ID
    // =====================================================

    private async Task<List<MediaFileModel>> GetFiles(
        int mediaId)
    {
        var files = new List<MediaFileModel>();

        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMediaFiles", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "GETBYMEDIAID",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaId", mediaId,
            SqlDbType.Int);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            files.Add(ReadMediaFile(reader));
        }

        return files;
    }

    // =====================================================
    // GET SINGLE FILE
    // =====================================================

    private async Task<MediaFileModel?> GetFileById(
        int mediaFileId)
    {
        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMediaFiles", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "GETBYID",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaFileId", mediaFileId,
            SqlDbType.Int);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return ReadMediaFile(reader);
    }

    // =====================================================
    // INSERT FILE RECORD
    // =====================================================

    private async Task<MediaFileModel> InsertFile(
        int mediaId,
        string mediaType,
        string mediaUrl,
        string? publicId)
    {
        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMediaFiles", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "INSERT",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaId", mediaId,
            SqlDbType.Int);

        AddParameter(
            cmd, "@MediaType", mediaType,
            SqlDbType.NVarChar, 20);

        AddParameter(
            cmd, "@MediaUrl", mediaUrl,
            SqlDbType.NVarChar, 2000);

        AddParameter(
            cmd, "@PublicId", publicId,
            SqlDbType.NVarChar, 300);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new Exception("File record insertion failed.");

        return ReadMediaFile(reader);
    }

    // =====================================================
    // UPDATE FILE RECORD
    // =====================================================

    private async Task UpdateFileRecord(
        int fileId,
        string mediaType,
        string mediaUrl,
        string? publicId)
    {
        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMediaFiles", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "UPDATE",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaFileId", fileId,
            SqlDbType.Int);

        AddParameter(
            cmd, "@MediaType", mediaType,
            SqlDbType.NVarChar, 20);

        AddParameter(
            cmd, "@MediaUrl", mediaUrl,
            SqlDbType.NVarChar, 2000);

        AddParameter(
            cmd, "@PublicId", publicId,
            SqlDbType.NVarChar, 300);

        await con.OpenAsync();

        await cmd.ExecuteNonQueryAsync();
    }

    // =====================================================
    // DELETE FILE RECORD
    // =====================================================

    private async Task DeleteFileRecord(int fileId)
    {
        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMediaFiles", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "DELETE",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaFileId", fileId,
            SqlDbType.Int);

        await con.OpenAsync();

        await cmd.ExecuteNonQueryAsync();
    }

    // =====================================================
    // CLOUDINARY UPLOAD
    // =====================================================

    private async Task<(string Url, string PublicId, string Type)>
        UploadToCloudinary(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new Exception("Please select a valid file.");

        if (file.Length > MaxFileSize)
            throw new Exception(
                "Maximum file size allowed is 50 MB.");

        string extension =
            Path.GetExtension(file.FileName).ToLowerInvariant();

        await using var stream = file.OpenReadStream();

        var fileDescription = new FileDescription(
            file.FileName,
            stream
        );

        if (ImageExtensions.Contains(extension))
        {
            var uploadParams = new ImageUploadParams
            {
                File = fileDescription,
                Folder = "airborne/gallery"
            };

            var result = await _cloudinary.UploadAsync(
                uploadParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);

            return (
                result.SecureUrl?.ToString()
                    ?? throw new Exception("Image URL missing."),
                result.PublicId
                    ?? throw new Exception("Image PublicId missing."),
                "Image"
            );
        }

        if (VideoExtensions.Contains(extension))
        {
            var uploadParams = new VideoUploadParams
            {
                File = fileDescription,
                Folder = "airborne/gallery"
            };

            var result = await _cloudinary.UploadAsync(
                uploadParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);

            return (
                result.SecureUrl?.ToString()
                    ?? throw new Exception("Video URL missing."),
                result.PublicId
                    ?? throw new Exception("Video PublicId missing."),
                "Video"
            );
        }

        throw new Exception(
            "Only JPG, JPEG, PNG, WEBP, GIF, MP4, WEBM and MOV allowed."
        );
    }

    // =====================================================
    // CLOUDINARY DELETE
    // =====================================================

    private async Task DeleteCloudinaryFile(
        MediaFileModel file)
    {
        if (string.IsNullOrWhiteSpace(file.PublicId))
            return;

        if (file.MediaType != "Image" &&
            file.MediaType != "Video")
            return;

        var deletionParams = new DeletionParams(
            file.PublicId)
        {
            ResourceType = file.MediaType == "Video"
                ? ResourceType.Video
                : ResourceType.Image
        };

        var result = await _cloudinary.DestroyAsync(
            deletionParams);

        if (result.Error != null)
            throw new Exception(
                "Cloudinary deletion failed: " +
                result.Error.Message);
    }

    // =====================================================
    // GET ALL GALLERIES WITH FILES
    // GET: api/Media
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var galleries = new List<MediaModel>();

        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMedia", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "GETALL",
            SqlDbType.NVarChar, 30);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            galleries.Add(new MediaModel
            {
                MediaId = Convert.ToInt32(reader["MediaId"]),
                MediaName = reader["MediaName"].ToString() ?? "",
                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                CategoryName = reader["CategoryName"].ToString() ?? "",
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
            });
        }

        foreach (var gallery in galleries)
        {
            gallery.Files = await GetFiles(gallery.MediaId);
        }

        return Ok(galleries);
    }

    // =====================================================
    // GET SINGLE GALLERY WITH FILES
    // GET: api/Media/5
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        MediaModel? gallery = null;

        using (var con = GetConnection())
        using (var cmd = new SqlCommand(
            "SP_ManageMedia", con))
        {
            cmd.CommandType = CommandType.StoredProcedure;

            AddParameter(
                cmd, "@Action", "GETBYID",
                SqlDbType.NVarChar, 30);

            AddParameter(
                cmd, "@MediaId", id,
                SqlDbType.Int);

            await con.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                gallery = new MediaModel
                {
                    MediaId = Convert.ToInt32(reader["MediaId"]),
                    MediaName = reader["MediaName"].ToString() ?? "",
                    CategoryId = Convert.ToInt32(reader["CategoryId"]),
                    CategoryName = reader["CategoryName"].ToString() ?? "",
                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                };
            }
        }

        if (gallery == null)
            return NotFound("Gallery not found.");

        gallery.Files = await GetFiles(id);

        return Ok(gallery);
    }

    // =====================================================
    // CREATE GALLERY
    // POST: api/Media
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] MediaModel model)
    {
        if (string.IsNullOrWhiteSpace(model.MediaName))
            return BadRequest("MediaName is required.");

        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMedia", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "INSERT",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaName", model.MediaName,
            SqlDbType.NVarChar, 200);

        AddParameter(
            cmd, "@CategoryId", model.CategoryId,
            SqlDbType.Int);

        AddParameter(
            cmd, "@IsActive", model.IsActive,
            SqlDbType.Bit);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return StatusCode(500, "Gallery creation failed.");

        int id = Convert.ToInt32(reader["MediaId"]);

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            new
            {
                message = "Gallery created successfully.",
                mediaId = id
            });
    }

    // =====================================================
    // UPDATE GALLERY DETAILS
    // PUT: api/Media/5
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] MediaModel model)
    {
        if (string.IsNullOrWhiteSpace(model.MediaName))
            return BadRequest("MediaName is required.");

        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMedia", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "UPDATE",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaId", id,
            SqlDbType.Int);

        AddParameter(
            cmd, "@MediaName", model.MediaName,
            SqlDbType.NVarChar, 200);

        AddParameter(
            cmd, "@CategoryId", model.CategoryId,
            SqlDbType.Int);

        AddParameter(
            cmd, "@IsActive", model.IsActive,
            SqlDbType.Bit);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return NotFound("Gallery not found.");

        return Ok(new
        {
            message = "Gallery updated successfully.",
            mediaId = id
        });
    }

    // =====================================================
    // MULTIPLE IMAGE / VIDEO UPLOAD
    // POST: api/Media/5/upload
    // =====================================================

    [HttpPost("{id:int}/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMultiple(
        int id,
         List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest("Please select files.");

        if (files.Any(f => f.Length > MaxFileSize))
            return BadRequest("Each file must be 50 MB or smaller.");

        // Verify gallery
        var gallery = await GetGalleryExists(id);

        if (!gallery)
            return NotFound("Gallery not found.");

        var uploaded = new List<MediaFileModel>();

        try
        {
            foreach (var file in files)
            {
                var upload = await UploadToCloudinary(file);

                try
                {
                    var saved = await InsertFile(
                        id,
                        upload.Type,
                        upload.Url,
                        upload.PublicId
                    );

                    uploaded.Add(saved);
                }
                catch
                {
                    // Remove orphaned Cloudinary upload
                    await DeleteCloudinaryFile(
                        new MediaFileModel
                        {
                            MediaType = upload.Type,
                            PublicId = upload.PublicId
                        });

                    throw;
                }
            }

            return Ok(new
            {
                message = "Files uploaded successfully.",
                mediaId = id,
                files = uploaded
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = ex.Message,
                uploadedFiles = uploaded
            });
        }
    }

    // =====================================================
    // CHECK GALLERY EXISTS
    // =====================================================

    private async Task<bool> GetGalleryExists(int id)
    {
        using var con = GetConnection();

        using var cmd = new SqlCommand(
            "SP_ManageMedia", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameter(
            cmd, "@Action", "GETBYID",
            SqlDbType.NVarChar, 30);

        AddParameter(
            cmd, "@MediaId", id,
            SqlDbType.Int);

        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync();
    }

    // =====================================================
    // ADD MULTIPLE EXTERNAL URLS
    // POST: api/Media/5/urls
    // =====================================================

    [HttpPost("{id:int}/urls")]
    public async Task<IActionResult> AddUrls(
        int id,
        [FromBody] MediaUrlRequest request)
    {
        if (!await GetGalleryExists(id))
            return NotFound("Gallery not found.");

        if (request.MediaId != id)
            return BadRequest("MediaId mismatch.");

        var saved = new List<MediaFileModel>();

        var allUrls = new List<(string Url, string Type)>();

        allUrls.AddRange(
            request.ImageUrls.Select(x => (x, "ImageUrl")));

        allUrls.AddRange(
            request.VideoUrls.Select(x => (x, "VideoUrl")));

        if (allUrls.Count == 0)
            return BadRequest("Please provide URLs.");

        foreach (var item in allUrls)
        {
            if (!Uri.TryCreate(
                item.Url,
                UriKind.Absolute,
                out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest(
                    "Invalid external URL: " + item.Url);
            }
        }

        foreach (var item in allUrls)
        {
            saved.Add(await InsertFile(
                id,
                item.Type,
                item.Url,
                null
            ));
        }

        return Ok(new
        {
            message = "External URLs added successfully.",
            files = saved
        });
    }

    // =====================================================
    // REPLACE EXISTING IMAGE / VIDEO
    // PUT: api/Media/file/10/replace
    // =====================================================

    [HttpPut("file/{fileId:int}/replace")]
    [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReplaceFile(
            int fileId,
            IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Replacement file required.");

        var oldFile = await GetFileById(fileId);

        if (oldFile == null)
            return NotFound("File not found.");

        if (oldFile.MediaType != "Image" &&
            oldFile.MediaType != "Video")
            return BadRequest(
                "Only Cloudinary Image/Video can be replaced.");

        var upload = await UploadToCloudinary(file);

        try
        {
            await UpdateFileRecord(
                fileId,
                upload.Type,
                upload.Url,
                upload.PublicId
            );
        }
        catch
        {
            await DeleteCloudinaryFile(
                new MediaFileModel
                {
                    MediaType = upload.Type,
                    PublicId = upload.PublicId
                });

            throw;
        }

        try
        {
            await DeleteCloudinaryFile(oldFile);
        }
        catch (Exception ex)
        {
            // New file is already saved. Report cleanup failure.
            return Ok(new
            {
                message = "File replaced, but old Cloudinary file cleanup failed.",
                warning = ex.Message,
                fileId
            });
        }

        return Ok(new
        {
            message = "File replaced successfully.",
            fileId,
            mediaUrl = upload.Url,
            mediaType = upload.Type
        });
    }

    // =====================================================
    // EDIT EXTERNAL URL
    // PUT: api/Media/file/10/url
    // =====================================================

    [HttpPut("file/{fileId:int}/url")]
    public async Task<IActionResult> EditExternalUrl(
        int fileId,
        [FromBody] MediaUrlRequest request)
    {
        var file = await GetFileById(fileId);

        if (file == null)
            return NotFound("File not found.");

        if (file.MediaType != "ImageUrl" &&
            file.MediaType != "VideoUrl" &&
            file.MediaType != "Url")
            return BadRequest("This is not an external URL.");

        string? newUrl = file.MediaType == "VideoUrl"
            ? request.VideoUrls.FirstOrDefault()
            : request.ImageUrls.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(newUrl) ||
            !Uri.TryCreate(newUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest("Valid external URL required.");
        }

        await UpdateFileRecord(
            fileId,
            file.MediaType,
            newUrl,
            null
        );

        return Ok(new
        {
            message = "External URL updated successfully."
        });
    }

    // =====================================================
    // DELETE INDIVIDUAL FILE
    // DELETE: api/Media/file/10
    // =====================================================

    [HttpDelete("file/{fileId:int}")]
    public async Task<IActionResult> DeleteFile(int fileId)
    {
        var file = await GetFileById(fileId);

        if (file == null)
            return NotFound("File not found.");

        // Delete Cloudinary file first
        await DeleteCloudinaryFile(file);

        // Then delete database record
        await DeleteFileRecord(fileId);

        return Ok(new
        {
            message = "File deleted successfully.",
            mediaFileId = fileId
        });
    }

    // =====================================================
    // DELETE COMPLETE GALLERY
    // DELETE: api/Media/5
    // =====================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteGallery(int id)
    {
        if (!await GetGalleryExists(id))
            return NotFound("Gallery not found.");

        var files = await GetFiles(id);

        // First delete all Cloudinary files
        foreach (var file in files)
        {
            await DeleteCloudinaryFile(file);
        }

        using var con = GetConnection();

        await con.OpenAsync();

        using var transaction = await con.BeginTransactionAsync();

        try
        {
            // Delete child records first
            using (var childCmd = new SqlCommand(
                "SP_ManageMediaFiles", con, (SqlTransaction)transaction))
            {
                childCmd.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    childCmd, "@Action", "DELETEBYMEDIAID",
                    SqlDbType.NVarChar, 30);

                AddParameter(
                    childCmd, "@MediaId", id,
                    SqlDbType.Int);

                await childCmd.ExecuteNonQueryAsync();
            }

            // Delete parent record
            using (var parentCmd = new SqlCommand(
                "SP_ManageMedia", con, (SqlTransaction)transaction))
            {
                parentCmd.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    parentCmd, "@Action", "DELETE",
                    SqlDbType.NVarChar, 30);

                AddParameter(
                    parentCmd, "@MediaId", id,
                    SqlDbType.Int);

                await parentCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Gallery and all its files deleted successfully.",
                mediaId = id,
                deletedFiles = files.Count
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
