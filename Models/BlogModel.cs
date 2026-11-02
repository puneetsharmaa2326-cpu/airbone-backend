using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class BlogModel
{
    public int BlogId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(220)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(500)]
    public string Excerpt { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ImageUrl { get; set; }
    public IFormFile? Image { get; set; }

    [StringLength(150)]
    public string Author { get; set; } = "Airborne Flight Desk";

    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
