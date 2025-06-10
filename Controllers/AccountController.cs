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
using realCabinly.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
                    ModelState.AddModelError("", "E-posta ve şifre gereklidir.");
                    return View();
                }

                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                bool isValidUser = false;
                string dbPasswordHash = null;
                string userRole = null;
                int userId = 0;

                string query = "SELECT id, password_hash, role FROM users WHERE lower(email) = lower(@Email)";
                _logger.LogInformation("Executing query to get user details for email: {Email}. Query: {QueryText}", email, query);

                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    _logger.LogInformation("DB connection opened for login attempt for email: {Email}", email);
                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Email", email);
                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetInt32(0);
                                dbPasswordHash = reader.IsDBNull(1) ? null : reader.GetString(1);
                                userRole = reader.IsDBNull(2) ? null : reader.GetString(2);
                                _logger.LogInformation("User found in DB for email: {Email}. UserID: {UserId}, Role: {UserRole}", email, userId, userRole);
                            }
                            else
                            {
                                _logger.LogWarning("User NOT found in DB for email: {Email}", email);
                            }
                        }
                    }
                }

                if (userId > 0 && dbPasswordHash != null && dbPasswordHash == password)
                {
                    isValidUser = true;
                }

                _logger.LogInformation("Login validation: isValidUser={IsValidUser} for email: {Email} (UserID: {UserId})", isValidUser, email, userId);

                if (isValidUser)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, email),
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, userRole)
                    };

                    _logger.LogInformation("Attempting to sign in user. Email: {Email}, UserID: {UserId}, Role: {UserRole}. Claims to be set:", email, userId, userRole);
                    foreach(var claim in claims)
                    {
                        _logger.LogInformation("- Claim Type: {ClaimType}, Claim Value: {ClaimValue}", claim.Type, claim.Value);
                    }

                    var identity = new ClaimsIdentity(claims, "MyCookieAuth");
                    var principal = new ClaimsPrincipal(identity);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    };

                    await HttpContext.SignInAsync("MyCookieAuth", principal, authProperties);
                    _logger.LogInformation("User {Email} (ID: {UserId}, Role: {UserRole}) signed in successfully.", email, userId, userRole);

                    if (userRole == "host")
                    {
                        _logger.LogInformation("Redirecting to Host/hostPage for host user: {Email}", email);
                        return RedirectToAction("hostPage", "Host");
                    }
                    else
                    {
                        _logger.LogInformation("Redirecting to Home/Index for non-host user: {Email} (Role: {UserRole})", email, userRole);
                        return RedirectToAction("Index", "Home");
                    }
                }

                ModelState.AddModelError("", "Geçersiz e-posta veya şifre.");
                _logger.LogWarning("Login failed: Invalid credentials for email: {Email}", email);
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login işlemi sırasında HATA oluştu for email: {Email}. Details: {ExceptionDetails}", email, ex.ToString());
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
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout işlemi sırasında hata oluştu");
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                _logger.LogWarning("Account/Settings GET: Kullanıcı kimliği alınamadı veya geçersiz.");
                return RedirectToAction("Login", "Account");
            }

            string userName = null;
            string userEmail = User.FindFirstValue(ClaimTypes.Email);
            string userRole = null;
            DateTime? createdAt = null;

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            try
            {
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT name, role, created_at FROM users WHERE id = @UserId";
                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userName = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"));
                                userRole = reader.IsDBNull(reader.GetOrdinal("role")) ? null : reader.GetString(reader.GetOrdinal("role"));
                                createdAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("created_at"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account/Settings GET: Kullanıcı adı çekilirken hata oluştu. UserId: {UserId}", userId);
                ViewBag.ProfileUpdateMessage = "Kullanıcı bilgileri yüklenirken bir hata oluştu.";
                ViewBag.ProfileUpdateSuccess = false;
            }

            var model = new UserSettingsViewModel
            {
                Name = userName
            };
            ViewBag.UserEmail = userEmail;
            ViewBag.UserRole = userRole;
            ViewBag.UserCreatedAt = createdAt?.ToString("dd MMMM yyyy, HH:mm") ?? "Bilgi yok";

            return View(model);
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
                TempData["ErrorMessage"] = "Tüm alanlar doldurulmalıdır.";
                return RedirectToAction("Settings", "Host");
            }

            if (newPassword != confirmNewPassword)
            {
                TempData["ErrorMessage"] = "Yeni şifreler eşleşmiyor.";
                return RedirectToAction("Settings", "Host");
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
                    if (dbPassword != null && dbPassword == currentPassword)
                    {
                        currentPasswordIsValid = true;
                    }
                }
            }

            if (!currentPasswordIsValid)
            {
                TempData["ErrorMessage"] = "Mevcut şifreniz yanlış.";
                return RedirectToAction("Settings", "Host");
            }

            // 2. Şifreyi güncelle
            string updatePasswordQuery = "UPDATE users SET password_hash = @NewPassword WHERE Email = @Email";
            int rowsAffected = 0;
            try
            {
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(updatePasswordQuery, connection))
                    {
                        command.Parameters.AddWithValue("@NewPassword", newPassword);
                        command.Parameters.AddWithValue("@Email", userEmail);
                        rowsAffected = await command.ExecuteNonQueryAsync();
                    }
                }

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Şifreniz başarıyla güncellendi. Lütfen tekrar giriş yapın.";
                    await HttpContext.SignOutAsync("MyCookieAuth"); // Oturumu sonlandır
                    return RedirectToAction("Login", "Account");
                }
                else
                {
                    TempData["ErrorMessage"] = "Şifre güncellenirken bir hata oluştu.";
                    return RedirectToAction("Settings", "Host");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangePassword sırasında veritabanı hatası.");
                TempData["ErrorMessage"] = "Şifre güncellenirken bir veritabanı hatası oluştu.";
                return RedirectToAction("Settings", "Host");
            }
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
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            _logger.LogInformation("Register POST metodu çağrıldı. Email: {Email}, Role: {Role}", model.Email, model.Role);

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Şifreler eşleşmiyor.");
                _logger.LogWarning("Şifreler eşleşmiyor. Email: {Email}", model.Email);
            }

            if (model.Role != "guest" && model.Role != "host")
            {
                ModelState.AddModelError("Role", "Geçersiz rol seçimi. 'guest' veya 'host' olmalıdır.");
                _logger.LogWarning("Geçersiz rol seçimi: {Role}. Beklenen 'guest' veya 'host'. Email: {Email}", model.Role, model.Email);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    string connectionString = _configuration.GetConnectionString("DefaultConnection");
                    using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        _logger.LogInformation("Veritabanı bağlantısı açıldı (Register). Email: {Email}", model.Email);

                        // Email'in zaten var olup olmadığını kontrol et
                        string checkUserSql = "SELECT COUNT(1) FROM users WHERE LOWER(email) = LOWER(@Email)";
                        using (NpgsqlCommand checkCmd = new NpgsqlCommand(checkUserSql, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@Email", model.Email.ToLower());
                            var userExists = (long)await checkCmd.ExecuteScalarAsync();
                            if (userExists > 0)
                            {
                                ModelState.AddModelError("Email", "Bu e-posta adresi zaten kayıtlı.");
                                _logger.LogWarning("Kayıt denemesi başarısız - E-posta zaten var: {Email}", model.Email);
                                return View(model);
                            }
                        }

                        // !!! GÜVENLİK UYARISI: ŞİFREYİ HASH'LEYİN !!!
                        string passwordToStore = model.Password;

                        // created_at veritabanı tarafından DEFAULT olarak ayarlandığı için sorgudan çıkarıldı.
                        // Sütun isimleri küçük harfe çevrildi (email, password_hash, role).
                        string sql = "INSERT INTO users (name, email, password_hash, role) VALUES (@Name, @Email, @PasswordHash, @Role)";
                        _logger.LogInformation("Çalıştırılacak SQL (Register): {SQLQuery}", sql);

                        using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@Name", model.Name);
                            command.Parameters.AddWithValue("@Email", model.Email);
                            command.Parameters.AddWithValue("@PasswordHash", passwordToStore);
                            command.Parameters.AddWithValue("@Role", model.Role);

                            int rowsAffected = await command.ExecuteNonQueryAsync();
                            _logger.LogInformation("ExecuteNonQueryAsync çalıştı (Register). Etkilenen satır sayısı: {RowsAffected}. Email: {Email}", rowsAffected, model.Email);

                            if (rowsAffected > 0)
                            {
                                _logger.LogInformation("Kullanıcı başarıyla kaydedildi: {Email}, Rol: {Role}. Login sayfasına yönlendiriliyor.", model.Email, model.Role);
                                TempData["RegistrationSuccessMessage"] = "Kaydınız başarıyla oluşturuldu. Şimdi giriş yapabilirsiniz.";
                                return RedirectToAction("Login", "Account"); // Yönlendirme AccountController'daki Login'e yapıldı
                            }
                            else
                            {
                                _logger.LogWarning("Veritabanına kullanıcı kaydı yapılamadı (etkilenen satır 0). Email: {Email}", model.Email);
                                ModelState.AddModelError("", "Kullanıcı kaydedilemedi. Lütfen tekrar deneyin.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kullanıcı kaydı sırasında HATA oluştu: {Email}. Detaylar: {ExceptionDetails}", model.Email, ex.ToString());
                    ModelState.AddModelError("", "Kayıt sırasında bir veritabanı hatası oluştu. Lütfen daha sonra tekrar deneyin.");
                }
            }
            else
            {
                _logger.LogWarning("ModelState geçerli değil (Register). Email: {Email}", model.Email);
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        _logger.LogWarning("- Anahtar: {Key}, Hata: {ErrorMessage} (Register)", entry.Key, error.ErrorMessage);
                    }
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserSettingsViewModel model)
        {
            ViewBag.UserEmail = User.FindFirstValue(ClaimTypes.Email); // E-postayı her zaman view'a gönder

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Account/UpdateProfile POST: ModelState geçerli değil.");
                ViewBag.ProfileUpdateMessage = "Lütfen adınızı doğru şekilde girin.";
                ViewBag.ProfileUpdateSuccess = false;
                // ModelState hatalarını da göstermek için Settings view'ını model ile döndür
                return View("Settings", model); 
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                _logger.LogWarning("Account/UpdateProfile POST: Kullanıcı kimliği alınamadı veya geçersiz.");
                ViewBag.ProfileUpdateMessage = "Oturumunuzla ilgili bir sorun oluştu. Lütfen tekrar giriş yapın.";
                ViewBag.ProfileUpdateSuccess = false;
                return View("Settings", model); 
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            try
            {
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE users SET name = @Name WHERE id = @UserId";
                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", model.Name);
                        command.Parameters.AddWithValue("@UserId", userId);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            _logger.LogInformation("Account/UpdateProfile POST: Kullanıcı {UserId} adı başarıyla güncellendi: {Name}", userId, model.Name);
                            ViewBag.ProfileUpdateMessage = "Adınız başarıyla güncellendi.";
                            ViewBag.ProfileUpdateSuccess = true;
                            // Kullanıcının cookie'sindeki Name claim'ini güncellemek için (eğer varsa ve kullanılıyorsa):
                            // Mevcut claim'leri al
                            var claimsPrincipal = User;
                            var identity = claimsPrincipal.Identity as ClaimsIdentity;
                            if (identity != null)
                            {
                                var nameClaim = identity.FindFirst(ClaimTypes.Name);
                                if (nameClaim != null && nameClaim.Value != model.Name) // Sadece gerçekten değiştiyse güncelle
                                {
                                    identity.RemoveClaim(nameClaim);
                                    identity.AddClaim(new Claim(ClaimTypes.Name, model.Name));
                                    // Güncellenmiş kimlikle tekrar oturum aç
                                    await HttpContext.SignInAsync("MyCookieAuth", new ClaimsPrincipal(identity));
                                    _logger.LogInformation("Account/UpdateProfile POST: User's Name claim updated in cookie.");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Account/UpdateProfile POST: Kullanıcı {UserId} için ad güncellenirken kayıt bulunamadı veya değişiklik olmadı.", userId);
                            ViewBag.ProfileUpdateMessage = "Adınız güncellenirken bir sorun oluştu veya mevcut adınızla aynı.";
                            ViewBag.ProfileUpdateSuccess = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account/UpdateProfile POST: Kullanıcı adı güncellenirken hata oluştu. UserId: {UserId}", userId);
                ViewBag.ProfileUpdateMessage = "Adınız güncellenirken bir veritabanı hatası oluştu.";
                ViewBag.ProfileUpdateSuccess = false;
            }
            // Her durumda Settings view'ını model ile ve güncel ViewBag mesajlarıyla döndür
            return View("Settings", model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "E-posta adresi gereklidir.");
                return View();
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                bool userExists = false;

                string query = "SELECT COUNT(1) FROM users WHERE LOWER(email) = LOWER(@Email)";
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Email", email.ToLower());
                        userExists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                    }
                }

                if (userExists)
                {
                    // E-posta gönderme işlemi
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("Cabinly", "cabinlyauth@gmail.com")); // Gönderen e-posta adresi
                    message.To.Add(new MailboxAddress("", email));
                    message.Subject = "Şifre Sıfırlama";
                    message.Body = new TextPart("plain")
                    {
                        Text = "Merhaba,\n\n" +
                              "Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:\n" +
                              "http://localhost:5006/Account/ResetPassword?email=" + email + "\n\n" +
                              "Bu işlemi siz yapmadıysanız, lütfen bu e-postayı dikkate almayın.\n\n" +
                              "Saygılarımızla,\nCabinly Ekibi"
                    };

                    using (var client = new SmtpClient())
                    {
                        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                        await client.AuthenticateAsync("cabinlyauth@gmail.com", "ouoy kawo thtu cfpr"); // Gmail uygulama şifresi
                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);
                    }

                    TempData["SuccessMessage"] = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Bu e-posta adresi ile kayıtlı bir kullanıcı bulunamadı.");
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama işlemi sırasında hata oluştu. Email: {Email}", email);
                ModelState.AddModelError("", "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
                return View();
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError("", "Tüm alanlar gereklidir.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Şifreler eşleşmiyor.");
                return View();
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                string updateQuery = "UPDATE users SET password_hash = @NewPassword WHERE LOWER(email) = LOWER(@Email)";
                
                await using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@NewPassword", newPassword);
                        command.Parameters.AddWithValue("@Email", email);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
                            return RedirectToAction("Login");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Şifre güncellenirken bir hata oluştu.");
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama işlemi sırasında hata oluştu. Email: {Email}", email);
                ModelState.AddModelError("", "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
                return View();
            }
        }
    }
}

namespace realCabinly.Models
{
    public class RegisterViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Adınız gereklidir.")]
        public string Name { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "E-posta adresi gereklidir.")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Şifre gereklidir.")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string Password { get; set; }

        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
        public string ConfirmPassword { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Rol seçimi gereklidir.")]
        public string Role { get; set; } // user ya da host
    }
}
