

using System.ComponentModel.DataAnnotations;

namespace AirborneEnquiryApi.Models
{
    public class EnquiryModel
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[0-9]{10}$",
            ErrorMessage = "Mobile number must be 10 digits.")]
        public string Mobile { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [StringLength(100)]
        public string Service { get; set; } = string.Empty;

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

        public DateTime? TravelDate { get; set; }

        public string? Passengers { get; set; }

        public string? TripDetails { get; set; }
    }
}
