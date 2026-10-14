using System.ComponentModel.DataAnnotations;

namespace EventPlatform.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Kullanıcı adı veya e-posta gerekli.")]
    public string Identifier { get; set; } = null!;

    [Required(ErrorMessage = "Şifre gerekli.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    public bool RememberMe { get; set; }
}