using System.ComponentModel.DataAnnotations;

namespace MediaMasterApi.Models
{
    public class MediaModel
    {
        public int MediaId { get; set; }

        [Required]
        [StringLength(200)]
        public string MediaName { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Image|Video)$")]
        public string MediaType { get; set; } = "Image";

        [Required]
        public string MediaUrl { get; set; } = string.Empty;

        public string PublicId { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; }
    }

    public class MediaStatusRequest
    {
        public bool IsActive { get; set; }
    }
}