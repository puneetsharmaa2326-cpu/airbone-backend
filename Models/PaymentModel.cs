using System.ComponentModel.DataAnnotations;

namespace AirborneEnquiryApi.Models
{


  
        public class PaymentModel
        {
            public int PaymentId { get; set; }

            public string FullName { get; set; } = "";

            public string MobileNumber { get; set; } = "";

            public string Email { get; set; } = "";

            public string Service { get; set; } = "";

            public string BookingReference { get; set; } = "";

            public decimal AmountPaid { get; set; }

            public string UTRNumber { get; set; } = "";

            public DateTime CreatedAt { get; set; }
        }
    


  
        public class PaymentRequest
        {
            [Required]
            [StringLength(150)]
            public string FullName { get; set; } = "";

            [Required]
            [StringLength(20)]
            public string MobileNumber { get; set; } = "";

            [Required]
            [EmailAddress]
            [StringLength(150)]
            public string Email { get; set; } = "";

            [Required]
            [StringLength(100)]
            public string Service { get; set; } = "";

            [Required]
            [StringLength(100)]
            public string BookingReference { get; set; } = "";

            [Range(typeof(decimal), "0.01", "9999999999999999.99")]
            public decimal AmountPaid { get; set; }

            [Required]
            [StringLength(100)]
            public string UTRNumber { get; set; } = "";
        }
    


}