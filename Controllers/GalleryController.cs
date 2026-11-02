using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MediaMasterApi.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GalleryController : ControllerBase
{
    private readonly string _connectionString;

    public GalleryController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    }

    private static void AddParameters(SqlCommand cmd, string action, int? id = null)
    {
        cmd.Parameters.Add("@Action", SqlDbType.NVarChar, 20).Value = action;
        cmd.Parameters.Add("@MediaId", SqlDbType.Int).Value = id.HasValue ? id.Value : DBNull.Value;
        cmd.Parameters.Add("@MediaName", SqlDbType.NVarChar, 200).Value = DBNull.Value;
        cmd.Parameters.Add("@MediaType", SqlDbType.NVarChar, 20).Value = "Image";
        cmd.Parameters.Add("@MediaUrl", SqlDbType.NVarChar, 1000).Value = DBNull.Value;
        cmd.Parameters.Add("@PublicId", SqlDbType.NVarChar, 300).Value = DBNull.Value;
        cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = DBNull.Value;
    }

    private static MediaModel ReadMedia(SqlDataReader reader) => new()
    {
        MediaId = Convert.ToInt32(reader["MediaId"]),
        MediaName = Convert.ToString(reader["MediaName"]) ?? string.Empty,
        MediaType = Convert.ToString(reader["MediaType"]) ?? string.Empty,
        MediaUrl = Convert.ToString(reader["MediaUrl"]) ?? string.Empty,
        PublicId = Convert.ToString(reader["PublicId"]) ?? string.Empty,
        IsActive = Convert.ToBoolean(reader["IsActive"]),
        CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
    };

    // GET /api/Gallery - public website gallery, active images only
    [HttpGet]
    public async Task<IActionResult> GetGallery()
    {
        try
        {
            var items = new List<MediaModel>();
            await using var con = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("dbo.ManageMedia", con)
            { CommandType = CommandType.StoredProcedure };
            AddParameters(cmd, "GETACTIVE");
            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = ReadMedia(reader);
                if (item.IsActive && string.Equals(item.MediaType, "Image", StringComparison.OrdinalIgnoreCase))
                    items.Add(item);
            }
            return Ok(items);
        }
        catch (SqlException)
        {
            return StatusCode(500, new { message = "Gallery load nahi hui. Existing dbo.ManageMedia procedure check karein." });
        }
    }

    // GET /api/Gallery/{id} - public active image details
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGalleryImage(int id)
    {
        if (id <= 0) return BadRequest(new { message = "Valid MediaId required." });
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand("dbo.ManageMedia", con)
            { CommandType = CommandType.StoredProcedure };
            AddParameters(cmd, "GETBYID", id);
            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return NotFound(new { message = "Gallery image not found." });
            var item = ReadMedia(reader);
            if (!item.IsActive || !string.Equals(item.MediaType, "Image", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = "Active gallery image not found." });
            return Ok(item);
        }
        catch (SqlException)
        {
            return StatusCode(500, new { message = "Gallery image load nahi hui." });
        }
    }
}
