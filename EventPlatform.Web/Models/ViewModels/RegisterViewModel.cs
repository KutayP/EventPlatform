using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EventPlatform.Web.Models.ViewModels;

public class RegisterViewModel
{
    [Required]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3 ile 50 karakter arasında olmalı.")]
    [RegularExpression(ValidationPatterns.Username, ErrorMessage = ValidationPatterns.UsernameMessage)]
    public string Username { get; set; } = null!;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = null!;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Required, StringLength(50)]
    [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
    public string FirstName { get; set; } = null!;

    [Required, StringLength(50)]
    [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
    public string LastName { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; }

    [Required]
    [AllowedValues("Male", "Female", "NonBinary", "Other")]
    public string Gender { get; set; } = null!;

    // Formdan gelen seçili ilgi alanı id'leri (isteğe bağlı).
    public List<int> SelectedInterestIds { get; set; } = new();

    // Formu çizmek için veritabanından doldurulur, istemciden asla bağlanmaz.
    [BindNever, ValidateNever]
    public List<InterestOption> Interests { get; set; } = new();
}