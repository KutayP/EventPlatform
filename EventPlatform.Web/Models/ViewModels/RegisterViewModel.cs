using System.ComponentModel.DataAnnotations;

namespace EventPlatform.Web.Models.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(50)]
    public string Username { get; set; } = null!;

    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Required, StringLength(50)]
    public string FirstName { get; set; } = null!;

    [Required, StringLength(50)]
    public string LastName { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    [Required]
    [AllowedValues("Male", "Female", "NonBinary", "Other")]
    public string Gender { get; set; } = null!;
}