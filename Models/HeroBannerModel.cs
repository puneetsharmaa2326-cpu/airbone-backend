using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class HeroBannerModel
    {
        public int BannerId { get; set; }

        public string BannerTitle { get; set; } = "";

        public string MediaType { get; set; } = "";

        public string MediaUrl { get; set; } = "";

        public string PublicId { get; set; } = "";

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }



    public class HeroBannerUploadRequest
    {
        [Required]
        public string BannerTitle { get; set; } = "";

        [Required]
        public string MediaType { get; set; } = "";

        public bool IsActive { get; set; } = true;

        [Required]
        public IFormFile File { get; set; } = null!;
    }

    public class HeroBannerStatusRequest
    {
        public bool IsActive { get; set; }
    }
}