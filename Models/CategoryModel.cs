
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class CategoryModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime? CreatedDate { get; set; }
    }
}
