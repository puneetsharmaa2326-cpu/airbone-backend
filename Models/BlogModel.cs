
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Models
{
    public class BlogModel
    {
        public int BlogId { get; set; }

        [Required(ErrorMessage = "Blog title is required.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Blog slug is required.")]
        [StringLength(220)]
        public string Slug { get; set; } = string.Empty;

        //[StringLength(100)]
        //public string Category { get; set; } = string.Empty;

        //[StringLength(500)]
        //public string Excerpt { get; set; } = string.Empty;

        public string? Excerpt { get; set; }

        public string? Category { get; set; }

        [Required(ErrorMessage = "Blog content is required.")]
        public string Content { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? ImageUrl { get; set; }

        // Uploaded image file
        //public IFormFile? Image { get; set; }
        // BAAD MEIN (add karo):
        public List<IFormFile>? Image { get; set; }

        [StringLength(150)]
        public string Author { get; set; } = "Airborne Flight Desk";

        public bool IsPublished { get; set; }

        public DateTime? PublishedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
