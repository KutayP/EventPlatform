using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EventPlatform.Web.Models.ViewModels;

public class ProfileViewModel
{
    // Sadece gösterilir, değiştirilemez.
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public int CreatedCount { get; set; }
    public int JoinedCount { get; set; }

    public ProfileInfoInput InfoForm { get; set; } = new();
    public ChangePasswordInput PasswordForm { get; set; } = new();

    public List<int> SelectedInterestIds { get; set; } = new();

    [BindNever, ValidateNever]
    public List<InterestOption> Interests { get; set; } = new();
}

public class ProfileInfoInput
{
    [Required(ErrorMessage = "Ad gerekli."), StringLength(50)]
    [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
    public string Firstname { get; set; } = "";

    [Required(ErrorMessage = "Soyad gerekli."), StringLength(50)]
    [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
    public string Lastname { get; set; } = "";

    [Required(ErrorMessage = "Doğum tarihi gerekli.")]
    [DataType(DataType.Date)]
    public DateTime Birthdate { get; set; }

    [Required(ErrorMessage = "Cinsiyet seçin.")]
    [AllowedValues("Male", "Female", "NonBinary", "Other")]
    public string Gender { get; set; } = "";
}

public class ChangePasswordInput
{
    [Required(ErrorMessage = "Mevcut şifre gerekli.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Yeni şifre gerekli.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Yeni şifre en az 8 karakter olmalı.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Yeni şifreyi tekrar girin.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}