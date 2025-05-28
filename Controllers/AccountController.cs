using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using Microsoft.AspNetCore.Authorization;

namespace realCabinly.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;

        public AccountController(ILogger<AccountController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("", "Kullanıcı adı ve şifre gereklidir");
                    return View();
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                bool isValidUser = false;

                string query = $"SELECT password_hash FROM users WHERE Email = '{email}'";
                _logger.LogInformation("Executing query: {Query}", query);

                string dbPasswordHash = null;

                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            dbPasswordHash = result.ToString();
                        }
                    }
                }

                // GÜVENLİK UYARISI: Şifreleri düz metin olarak saklamak ve karşılaştırmak SON DERECE GÜVENSİZDİR!
                // Bu yöntem KESİNLİKLE üretim ortamlarında kullanılmamalıdır.
                // Şifreler her zaman hash'lenerek saklanmalı ve hash'ler karşılaştırılmalıdır.
                if (dbPasswordHash != null && dbPasswordHash == password) // Güncellendi: Doğrudan karşılaştırma
                {
                    isValidUser = true;
                }

                _logger.LogInformation("isValidUser: {IsValidUser} for email: {Email} (DB Value: {DbValue})", isValidUser, email, dbPasswordHash);

                if (isValidUser)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, email),
                    };

                    var identity = new ClaimsIdentity(claims, "MyCookieAuth");
                    var principal = new ClaimsPrincipal(identity);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    };

                    await HttpContext.SignInAsync("MyCookieAuth", principal, authProperties);
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login işlemi sırasında hata oluştu");
                ModelState.AddModelError("", "Giriş yapılırken bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync("MyCookieAuth");
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout işlemi sırasında hata oluştu");
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult Settings()
        {
            // İleride kullanıcı ayarlarıyla ilgili model verisi buraya eklenebilir.
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmail(string currentPassword, string newEmail)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newEmail))
            {
                ViewBag.EmailChangeMessage = "Tüm alanlar doldurulmalıdır.";
                ViewBag.EmailChangeSuccess = false;
                return View("Settings");
            }

            var userEmail = User.Identity.Name;
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            bool currentPasswordIsValid = false;

            string passwordQuery = "SELECT password_hash FROM users WHERE Email = @Email";
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using (var command = new NpgsqlCommand(passwordQuery, connection))
                {
                    command.Parameters.AddWithValue("@Email", userEmail);
                    var dbPassword = (await command.ExecuteScalarAsync())?.ToString();
                    if (dbPassword != null && dbPassword == currentPassword)
                    {
                        currentPasswordIsValid = true;
                    }
                }
            }

            if (!currentPasswordIsValid)
            {
                ViewBag.EmailChangeMessage = "Mevcut şifreniz yanlış.";
                ViewBag.EmailChangeSuccess = false;
                return View("Settings");
            }

            string emailExistsQuery = "SELECT COUNT(1) FROM users WHERE Email = @NewEmail";
            bool emailExists = false;
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using (var command = new NpgsqlCommand(emailExistsQuery, connection))
                {
                    command.Parameters.AddWithValue("@NewEmail", newEmail);
                    emailExists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }

            if (emailExists)
            {
                ViewBag.EmailChangeMessage = "Bu e-posta adresi zaten başka bir kullanıcı tarafından kullanılıyor.";
                ViewBag.EmailChangeSuccess = false;
                return View("Settings");
            }

            string updateEmailQuery = "UPDATE users SET Email = @NewEmail WHERE Email = @OldEmail";
            int rowsAffected = 0;
            try
            {
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(updateEmailQuery, connection))
                    {
                        command.Parameters.AddWithValue("@NewEmail", newEmail);
                        command.Parameters.AddWithValue("@OldEmail", userEmail);
                        rowsAffected = await command.ExecuteNonQueryAsync();
                    }
                }

                if (rowsAffected > 0)
                {
                    ViewBag.EmailChangeMessage = "E-posta adresiniz başarıyla güncellendi. Değişikliklerin geçerli olması için lütfen tekrar giriş yapın.";
                    ViewBag.EmailChangeSuccess = true;
                    await HttpContext.SignOutAsync("MyCookieAuth");
                }
                else
                {
                    ViewBag.EmailChangeMessage = "E-posta güncellenirken bir hata oluştu.";
                    ViewBag.EmailChangeSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangeEmail sırasında veritabanı hatası.");
                ViewBag.EmailChangeMessage = "E-posta güncellenirken bir veritabanı hatası oluştu.";
                ViewBag.EmailChangeSuccess = false;
            }
            
            return View("Settings");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
        {
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmNewPassword))
            {
                ViewBag.PasswordChangeMessage = "Tüm alanlar doldurulmalıdır.";
                ViewBag.PasswordChangeSuccess = false;
                return View("Settings");
            }

            if (newPassword != confirmNewPassword)
            {
                ViewBag.PasswordChangeMessage = "Yeni şifreler eşleşmiyor.";
                ViewBag.PasswordChangeSuccess = false;
                return View("Settings");
            }

            var userEmail = User.Identity.Name;
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            bool currentPasswordIsValid = false;

            // 1. Mevcut şifreyi kontrol et
            string passwordQuery = "SELECT password_hash FROM users WHERE Email = @Email";
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using (var command = new NpgsqlCommand(passwordQuery, connection))
                {
                    command.Parameters.AddWithValue("@Email", userEmail);
                    var dbPassword = (await command.ExecuteScalarAsync())?.ToString();
                    // GÜVENLİK UYARISI: Düz metin şifre karşılaştırması!
                    if (dbPassword != null && dbPassword == currentPassword)
                    {
                        currentPasswordIsValid = true;
                    }
                }
            }

            if (!currentPasswordIsValid)
            {
                ViewBag.PasswordChangeMessage = "Mevcut şifreniz yanlış.";
                ViewBag.PasswordChangeSuccess = false;
                return View("Settings");
            }

            // 2. Şifreyi güncelle (GÜVENLİK UYARISI: Yeni şifre düz metin olarak kaydediliyor! Hash'lenmeli!)
            string updatePasswordQuery = "UPDATE users SET password_hash = @NewPassword WHERE Email = @Email";
            int rowsAffected = 0;
            try
            {
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(updatePasswordQuery, connection))
                    {
                        // GERÇEK UYGULAMADA @NewPassword PARAMETRESİNE YENİ ŞİFRENİN HASH'LENMİŞ HALİ GÖNDERİLMELİDİR.
                        command.Parameters.AddWithValue("@NewPassword", newPassword); // Düz metin olarak kaydediliyor!
                        command.Parameters.AddWithValue("@Email", userEmail);
                        rowsAffected = await command.ExecuteNonQueryAsync();
                    }
                }

                if (rowsAffected > 0)
                {
                    ViewBag.PasswordChangeMessage = "Şifreniz başarıyla güncellendi. Lütfen tekrar giriş yapın.";
                    ViewBag.PasswordChangeSuccess = true;
                    await HttpContext.SignOutAsync("MyCookieAuth"); // Oturumu sonlandır
                }
                else
                {
                    ViewBag.PasswordChangeMessage = "Şifre güncellenirken bir hata oluştu.";
                    ViewBag.PasswordChangeSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangePassword sırasında veritabanı hatası.");
                ViewBag.PasswordChangeMessage = "Şifre güncellenirken bir veritabanı hatası oluştu.";
                ViewBag.PasswordChangeSuccess = false;
            }

            return View("Settings");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string name, string email, string password, string confirmPassword)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError("", "Tüm alanlar doldurulmalıdır.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Şifreler eşleşmiyor.");
                return View();
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            string emailExistsQuery = "SELECT COUNT(1) FROM users WHERE Email = @Email";
            bool emailExists = false;

            try
            {
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(emailExistsQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Email", email);
                        emailExists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                    }
                }

                if (emailExists)
                {
                    ModelState.AddModelError("", "Bu e-posta adresi zaten kullanılıyor.");
                    return View();
                }

                // Kullanıcı adı (name) kolonunun veritabanındaki adının 'name' (küçük harf) olduğunu varsayıyoruz.
                // Kullanıcı tarafından yapılan son değişikliğe göre güncellendi.
                string insertUserQuery = "INSERT INTO users (name, Email, password_hash) VALUES (@Name, @Email, @Password)";
                int rowsAffected = 0;
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(insertUserQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Email", email);
                        command.Parameters.AddWithValue("@Password", password);
                        rowsAffected = await command.ExecuteNonQueryAsync();
                    }
                }

                if (rowsAffected > 0)
                {
                    TempData["RegistrationSuccessMessage"] = "Kaydınız başarıyla oluşturuldu. Şimdi giriş yapabilirsiniz.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register POST sırasında veritabanı hatası.");
                ModelState.AddModelError("", "Kayıt sırasında beklenmedik bir sunucu hatası oluştu.");
            }

            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
