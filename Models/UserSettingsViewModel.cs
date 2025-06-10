using System.ComponentModel.DataAnnotations;

namespace realCabinly.Models
{
    public class UserSettingsViewModel
    {
        [Required(ErrorMessage = "Ad ve soyad gereklidir.")]
        [Display(Name = "Ad Soyad")]
        public string Name { get; set; }

        // E-posta bu modelde doğrudan güncellenmiyor, AccountController'daki
        // ChangeEmail metodu üzerinden yönetiliyor.
        // public string Email { get; set; }
    }
} 