
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MediaMasterApi.Models
{
    public class MediaUploadRequest
    {
        [Required]
        public string MediaName { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public string MediaType { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
