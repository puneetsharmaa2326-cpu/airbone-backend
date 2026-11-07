
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    // =========================================
    // 1. GALLERY MAIN MODEL
    // =========================================

    public class MediaModel
    {
        public int MediaId { get; set; }

        [Required(ErrorMessage = "Gallery name is required.")]
        [StringLength(200)]
        public string MediaName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        public int CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedDate { get; set; }

        public int TotalFiles { get; set; }

        public List<MediaFileModel> Files { get; set; } = new();
    }


    // =========================================
    // 2. GALLERY FILE MODEL
    // =========================================

    public class MediaFileModel
    {
        public int MediaFileId { get; set; }

        public int MediaId { get; set; }

        // Image, Video, ImageUrl, VideoUrl
        public string MediaType { get; set; } = string.Empty;

        public string MediaUrl { get; set; } = string.Empty;

        // Cloudinary PublicId
        public string? PublicId { get; set; }

        public DateTime? CreatedDate { get; set; }
    }


    // =========================================
    // 3. MULTIPLE FILE UPLOAD MODEL
    // POST: api/Media/{id}/upload
    // =========================================

    public class MediaUploadRequest
    {
        public List<IFormFile> Files { get; set; } = new();
    }


    // =========================================
    // 4. EXTERNAL URL MODEL
    // POST: api/Media/{id}/urls
    // PUT: api/Media/file/{fileId}/url
    // =========================================

    public class MediaUrlRequest
    {
        [Required]
        public int MediaId { get; set; }

        public List<string> ImageUrls { get; set; } = new();

        public List<string> VideoUrls { get; set; } = new();
    }


    // =========================================
    // 5. GALLERY UPDATE MODEL
    // =========================================

    public class MediaUpdateRequest
    {
        [Required]
        public int MediaId { get; set; }

        [Required(ErrorMessage = "Gallery name is required.")]
        [StringLength(200)]
        public string MediaName { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        public bool IsActive { get; set; } = true;
    }


    // =========================================
    // 6. SINGLE FILE REPLACEMENT MODEL
    // PUT: api/Media/file/{fileId}/replace
    // =========================================

    public class MediaFileReplaceRequest
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }


    // =========================================
    // 7. CATEGORY MODEL
    // =========================================

    public class MediaCategoryModel
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;
    }
}
