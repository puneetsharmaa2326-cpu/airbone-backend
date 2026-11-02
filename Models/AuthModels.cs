using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public sealed class SignUpRequest
{
    [Required, StringLength(80, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "LoginId may contain letters, numbers, dot, underscore and hyphen only.")]
    public string LoginId { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, StringLength(80)] public string LoginId { get; set; } = string.Empty;
    [Required, StringLength(128)] public string Password { get; set; } = string.Empty;
}

public sealed class UpdatePasswordRequest
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = string.Empty;
}

public sealed class DeleteAccountRequest
{
    [Required, StringLength(128)] public string Password { get; set; } = string.Empty;
}
