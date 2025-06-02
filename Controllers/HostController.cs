using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
// using System.Data.SqlClient; // SQL Server için
using Npgsql; // PostgreSQL için eklendi
using System;
using Microsoft.Extensions.Configuration;
using System.Security.Claims; // Kullanıcı kimliği için eklendi
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // DataAnnotations için eklendi
using Microsoft.AspNetCore.Authorization; // AuthorizeAttribute için eklendi
using realCabinly.Models; // UserSettingsViewModel ve Ilan (eğer o da taşınırsa) için eklendi
// using realCabinly.Models; // Kaldırıldı, Ilan sınıfı aşağıya eklendi. - Bu satır fazlalık, kaldırılacak

namespace realCabinly.Controllers;

// Kullanıcı Ayarları için ViewModel - BU KISIM TAŞINACAK/SİLİNECEK
// public class UserSettingsViewModel
// {
//     [Required(ErrorMessage = "Ad ve soyad gereklidir.")]
//     [Display(Name = "Ad Soyad")]
//     public string Name { get; set; }
//
//     // E-posta ve diğer hassas bilgiler genellikle AccountController altında yönetilir.
//     // Şimdilik sadece Name alanını ekliyoruz.
//     // public string Email { get; set; } // Eğer e-postayı da burada göstermek isterseniz.
// }

// Ilan sınıfını geçici olarak buraya taşıyoruz, linter hatasını çözmek için.
// İDEALDE BU SINIF Models/Ilan.cs DOSYASINDA VE realCabinly.Models NAMESPACE'İNDE OLMALIDIR.
public class Ilan
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public decimal PricePerNight { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CoverImageUrl { get; set; }
}

// İlan Düzenleme için ViewModel
public class AdEditViewModel
{
    public int Id { get; set; } // Listing ID

    [Required(ErrorMessage = "İlan başlığı gereklidir.")]
    [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    public string Title { get; set; }

    public string Description { get; set; }

    [Required(ErrorMessage = "Konum bilgisi gereklidir.")]
    [StringLength(255, ErrorMessage = "Konum en fazla 255 karakter olabilir.")]
    public string Location { get; set; }

    [Required(ErrorMessage = "Gecelik fiyat gereklidir.")]
    [Range(1, 100000, ErrorMessage = "Fiyat 1 ile 100.000 arasında olmalıdır.")]
    [DataType(DataType.Currency)]
    public decimal PricePerNight { get; set; }

    public bool IsActive { get; set; }
    
    public string CurrentCoverImageUrl { get; set; } // Mevcut kapak resmi yolu
    public IFormFile NewImage { get; set; } // Yeni yüklenecek resim
}

public class HostController : Controller
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HostController> _logger;

    public HostController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration, ILogger<HostController> logger)
    {
        _webHostEnvironment = webHostEnvironment;
        _configuration = configuration;
        _logger = logger;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Host/Index sayfası çağrıldı.");
        return View(); 
    }

    // GET
    public IActionResult activeReservations()
    {
        return View();
    }

    public IActionResult reservationHistory()
    {
        return View();
    }

    public IActionResult hostAboutPage()
    {
        return View();
    }

    public IActionResult hostContactPage()
    {
        return View();
    }

    public IActionResult hostPage()
    {
        return View();
    }

    public async Task<IActionResult> myAds()
    {
        _logger.LogInformation("myAds sayfası için kullanıcı ilanları çekiliyor.");
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            _logger.LogWarning("myAds: Kullanıcı kimliği alınamadı veya geçersiz.");
            // Kullanıcı girişi yapılmamışsa veya ID alınamıyorsa login'e yönlendirilebilir veya hata mesajı gösterilebilir.
            return RedirectToAction("Login", "Account"); 
        }

        _logger.LogInformation("myAds: Kullanıcı ID'si {UserId} için aktif ilanlar çekilecek.", userId);
        List<Ilan> ads = new List<Ilan>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // listings tablosundan user_id'ye ve is_active=true durumuna göre ilanları çek.
                // Her ilan için photos tablosundan is_cover=true olan ilk resmi (image_url) LEFT JOIN ile al.
                string sql = @"
                    SELECT 
                        l.id AS listing_id,
                        l.title,
                        l.description,
                        l.location,
                        l.price_per_night,
                        l.is_active,
                        l.created_at,
                        (SELECT p.image_url FROM photos p WHERE p.listing_id = l.id AND p.is_cover = TRUE LIMIT 1) AS cover_image_url
                    FROM 
                        listings l
                    WHERE 
                        l.user_id = @user_id AND l.is_active = TRUE
                    ORDER BY
                        l.created_at DESC;
                ";

                _logger.LogInformation("myAds: Çalıştırılacak SQL: {SQLQuery}", sql.Replace("\n", " ").Replace("\r", " "));
                using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ads.Add(new Ilan
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("listing_id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                Location = reader.GetString(reader.GetOrdinal("location")),
                                PricePerNight = reader.GetDecimal(reader.GetOrdinal("price_per_night")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CoverImageUrl = reader.IsDBNull(reader.GetOrdinal("cover_image_url")) ? "/images/default-ad-image.png" : reader.GetString(reader.GetOrdinal("cover_image_url"))
                            });
                        }
                        _logger.LogInformation("myAds: {AdCount} adet aktif ilan bulundu ve modellendi.", ads.Count);
                    }
                }
            }
        }
        catch (NpgsqlException pgEx)
        {
            _logger.LogError(pgEx, "myAds: İlanlar çekilirken PostgreSQL HATA oluştu. UserId: {UserId}", userId);
            // Hata durumunda boş liste veya hata mesajıyla view döndürülebilir.
            ViewBag.ErrorMessage = "İlanlarınız yüklenirken bir veritabanı hatası oluştu.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "myAds: İlanlar çekilirken genel HATA oluştu. UserId: {UserId}", userId);
            ViewBag.ErrorMessage = "İlanlarınız yüklenirken beklenmedik bir hata oluştu.";
        }

        return View(ads); // List<MyAdViewModel> modelini view'a gönder
    }

    public IActionResult newAd()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAd(string adTitle, string adDescription, decimal adDailyPrice, IFormFile adImg, string location)
    {
        _logger.LogInformation("CreateAd metodu çağrıldı. Form verileri: Title='{AdTitle}', Location='{Location}', Price='{AdDailyPrice}', Image provided: {HasImage}", 
            adTitle, location, adDailyPrice, adImg != null && adImg.Length > 0);

        if (ModelState.IsValid && !string.IsNullOrEmpty(location) && adImg != null && adImg.Length > 0)
        {
            _logger.LogInformation("ModelState geçerli, konum bilgisi ve resim dolu.");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            NpgsqlTransaction transaction = null;
            string relativeImagePath = null;
            string absoluteImagePath = null;

            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    transaction = await connection.BeginTransactionAsync();
                    _logger.LogInformation("Veritabanı bağlantısı açıldı ve transaction başlatıldı.");

                    var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                    {
                        _logger.LogWarning("Kullanıcı kimliği alınamadı veya geçersiz.");
                        ModelState.AddModelError("", "Oturum açmış kullanıcı kimliği alınamadı veya geçersiz.");
                        await transaction.RollbackAsync();
                        return View("newAd", new { adTitle, adDescription, adDailyPrice, location });
                    }
                    _logger.LogInformation("Kullanıcı ID'si: {UserId}", userId);

                    string listingsSql = "INSERT INTO listings (user_id, title, description, location, price_per_night, is_active) " +
                                         "VALUES (@user_id, @title, @description, @location, @price_per_night, @is_active) RETURNING id";
                    int newListingId = 0;
                    
                    _logger.LogInformation("Çalıştırılacak SQL (listings): {SQLQuery}", listingsSql);
                    using (NpgsqlCommand listingsCommand = new NpgsqlCommand(listingsSql, connection, transaction))
                    {
                        listingsCommand.Parameters.AddWithValue("@user_id", userId);
                        listingsCommand.Parameters.AddWithValue("@title", adTitle);
                        listingsCommand.Parameters.AddWithValue("@description", adDescription ?? (object)DBNull.Value);
                        listingsCommand.Parameters.AddWithValue("@location", location);
                        listingsCommand.Parameters.AddWithValue("@price_per_night", adDailyPrice);
                        listingsCommand.Parameters.AddWithValue("@is_active", true);
                        
                        object returnedId = await listingsCommand.ExecuteScalarAsync();
                        if (returnedId == null || !int.TryParse(returnedId.ToString(), out newListingId) || newListingId <= 0)
                        {
                            _logger.LogError("listings tablosuna kayıt sonrası geçerli bir ID alınamadı.");
                            ModelState.AddModelError("", "İlan ana bilgileri kaydedilirken bir sorun oluştu.");
                            await transaction.RollbackAsync();
                            return View("newAd", new { adTitle, adDescription, adDailyPrice, location });
                        }
                        _logger.LogInformation("listings tablosuna kayıt BAŞARILI. Yeni listing_id: {NewListingId}", newListingId);
                    }

                    string wwwRootPath = _webHostEnvironment.WebRootPath;
                    string imageDir = Path.Combine(wwwRootPath, "images", "listings");
                    if (!Directory.Exists(imageDir)) Directory.CreateDirectory(imageDir);

                    string extension = Path.GetExtension(adImg.FileName);
                    string safeFileName = Path.GetRandomFileName().Replace(".", "") + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + extension;
                    relativeImagePath = "/" + Path.Combine("images", "listings", safeFileName).Replace('\\', '/');
                    absoluteImagePath = Path.Combine(wwwRootPath, relativeImagePath.TrimStart('/'));
                    
                    _logger.LogInformation("Resim kaydedilecek: {AbsolutePath}", absoluteImagePath);
                    using (var fileStream = new FileStream(absoluteImagePath, FileMode.Create))
                    {
                        await adImg.CopyToAsync(fileStream);
                    }
                    _logger.LogInformation("Resim başarıyla kaydedildi: {RelativePath}", relativeImagePath);

                    string photoSql = "INSERT INTO photos (listing_id, image_url, is_cover) " +
                                      "VALUES (@listing_id, @image_url, @is_cover)";
                    _logger.LogInformation("Çalıştırılacak SQL (photos): {SQLQuery}", photoSql);
                    using (NpgsqlCommand photoCommand = new NpgsqlCommand(photoSql, connection, transaction))
                    {
                        photoCommand.Parameters.AddWithValue("@listing_id", newListingId);
                        photoCommand.Parameters.AddWithValue("@image_url", relativeImagePath);
                        photoCommand.Parameters.AddWithValue("@is_cover", true);

                        int photoRowsAffected = await photoCommand.ExecuteNonQueryAsync();
                        if (photoRowsAffected <= 0)
                        {
                             _logger.LogError("photos tablosuna kayıt yapılamadı. listing_id: {ListingId}", newListingId);
                            ModelState.AddModelError("", "İlan resmi bilgileri kaydedilemedi.");
                            await transaction.RollbackAsync(); 
                            if (System.IO.File.Exists(absoluteImagePath)) System.IO.File.Delete(absoluteImagePath);
                            return View("newAd", new { adTitle, adDescription, adDailyPrice, location });
                        }
                        _logger.LogInformation("photos tablosuna kayıt BAŞARILI. listing_id: {ListingId}", newListingId);
                    }

                    await transaction.CommitAsync();
                    _logger.LogInformation("Transaction başarıyla commit edildi. İlan ve resim eklendi. Yönlendiriliyor: Host/hostPage");
                    TempData["SuccessMessage"] = "İlan ve resmi başarıyla oluşturuldu!";
                    return RedirectToAction("hostPage", "Host");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateAd işlemi sırasında HATA oluştu: {ExceptionDetails}", ex.ToString());
                if (transaction != null && transaction.Connection != null && transaction.Connection.State == System.Data.ConnectionState.Open)
                {
                    try 
                    {
                        await transaction.RollbackAsync(); 
                        _logger.LogInformation("Transaction rollback yapıldı."); 
                    }
                    catch (Exception rbEx) { _logger.LogError(rbEx, "Transaction rollback sırasında hata."); }
                }
                
                if (!string.IsNullOrEmpty(absoluteImagePath) && System.IO.File.Exists(absoluteImagePath))
                {
                    try { System.IO.File.Delete(absoluteImagePath); _logger.LogInformation("Hata nedeniyle yüklenen resim silindi: {ImagePath}", absoluteImagePath); }
                    catch (Exception delEx) { _logger.LogError(delEx, "Hata sonrası resim silinirken hata: {ImagePath}", absoluteImagePath); }
                }
                
                if (ex is NpgsqlException pgEx)
                {
                    ModelState.AddModelError("", "İlan oluşturulurken bir veritabanı hatası oluştu: " + pgEx.Message);
                }
                else
                {
                    ModelState.AddModelError("", "İlan oluşturulurken beklenmedik bir hata oluştu: " + ex.Message);
                }
            }
        }
        else
        {
            _logger.LogWarning("ModelState geçerli değil veya gerekli alanlar (konum, resim) eksik.");
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState Hataları:");
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        _logger.LogWarning("- Anahtar: {Key}, Hata: {ErrorMessage}", entry.Key, error.ErrorMessage);
                    }
                }
            }
            if (string.IsNullOrEmpty(location)) _logger.LogWarning("Konum bilgisi eksik.");
            if (adImg == null || adImg.Length == 0) _logger.LogWarning("İlan görseli eksik veya boyutu 0.");
        }
        
        _logger.LogInformation("CreateAd metodu sonlanıyor, View('newAd') döndürülüyor.");
        return View("newAd", new { adTitle, adDescription, adDailyPrice, location });
    }

    // GET: Host/EditAd/5
    [HttpGet]
    public async Task<IActionResult> EditAd(int id)
    {
        _logger.LogInformation("EditAd GET çağrıldı. İlan ID: {ListingId}", id);
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int currentUserId))
        {
            _logger.LogWarning("EditAd: Kullanıcı kimliği alınamadı veya geçersiz. Login sayfasına yönlendiriliyor.");
            return RedirectToAction("Login", "Account");
        }

        AdEditViewModel viewModel = null;
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT 
                        l.id, l.user_id, l.title, l.description, l.location, l.price_per_night, l.is_active,
                        (SELECT p.image_url FROM photos p WHERE p.listing_id = l.id AND p.is_cover = TRUE LIMIT 1) AS cover_image_url
                    FROM listings l
                    WHERE l.id = @listing_id;
                ";
                using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@listing_id", id);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // İlanın bu kullanıcıya ait olup olmadığını kontrol et
                            if (reader.GetInt32(reader.GetOrdinal("user_id")) != currentUserId)
                            {
                                _logger.LogWarning("EditAd: Kullanıcı {UserId}, kendisine ait olmayan ilanı ({ListingId}) düzenlemeye çalıştı.", currentUserId, id);
                                TempData["ErrorMessage"] = "Bu ilanı düzenleme yetkiniz yok.";
                                return RedirectToAction(nameof(myAds));
                            }

                            viewModel = new AdEditViewModel
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                Location = reader.GetString(reader.GetOrdinal("location")),
                                PricePerNight = reader.GetDecimal(reader.GetOrdinal("price_per_night")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CurrentCoverImageUrl = reader.IsDBNull(reader.GetOrdinal("cover_image_url")) ? "/images/default-ad-image.png" : reader.GetString(reader.GetOrdinal("cover_image_url"))
                            };
                        }
                    }
                }
            }

            if (viewModel == null)
            {
                _logger.LogWarning("EditAd: İlan ID {ListingId} bulunamadı.", id);
                TempData["ErrorMessage"] = "Düzenlenecek ilan bulunamadı.";
                return RedirectToAction(nameof(myAds));
            }
            _logger.LogInformation("EditAd: İlan ID {ListingId} bulundu ve model oluşturuldu. View döndürülüyor.", id);
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EditAd GET işlemi sırasında HATA oluştu. İlan ID: {ListingId}", id);
            TempData["ErrorMessage"] = "İlan bilgileri yüklenirken bir hata oluştu.";
            return RedirectToAction(nameof(myAds));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAd(AdEditViewModel model)
    {
        _logger.LogInformation("EditAd POST çağrıldı. İlan ID: {ListingId}, Başlık: {Title}", model.Id, model.Title);
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int currentUserId))
        {
            _logger.LogWarning("EditAd POST: Kullanıcı kimliği alınamadı veya geçersiz.");
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("EditAd POST: ModelState geçerli değil. İlan ID: {ListingId}", model.Id);
            foreach (var entry in ModelState)
            {
                if (entry.Value.Errors.Any())
                {
                    _logger.LogWarning("Alan: {Key}", entry.Key);
                    foreach (var error in entry.Value.Errors)
                    {
                        _logger.LogWarning("- Hata: {ErrorMessage}", error.ErrorMessage);
                        if (error.Exception != null)
                        {
                            _logger.LogWarning("- İstisna: {Exception}", error.Exception.Message);
                        }
                    }
                }
            }
            return View(model); 
        }

        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        NpgsqlTransaction transaction = null;
        string oldImagePathToDelete = null;
        string newRelativeImagePath = null;
        string newAbsoluteImagePath = null;

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                transaction = await connection.BeginTransactionAsync();

                // Önce ilanın kullanıcıya ait olup olmadığını ve var olup olmadığını tekrar kontrol edelim.
                int dbUserId = 0;
                string currentDbCoverImageUrl = null;
                using (var checkCmd = new NpgsqlCommand("SELECT user_id, (SELECT image_url FROM photos ph WHERE ph.listing_id = l.id AND ph.is_cover = TRUE LIMIT 1) as cover_url FROM listings l WHERE id = @id", connection, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@id", model.Id);
                    using (var reader = await checkCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            dbUserId = reader.GetInt32(0);
                            if (!reader.IsDBNull(1)) currentDbCoverImageUrl = reader.GetString(1);
                        }
                        else
                        {
                            _logger.LogWarning("EditAd POST: Güncellenmek istenen ilan ({ListingId}) bulunamadı.", model.Id);
                            TempData["ErrorMessage"] = "Güncellenmek istenen ilan bulunamadı.";
                            await transaction.RollbackAsync();
                            return RedirectToAction(nameof(myAds));
                        }
                    }
                }

                if (dbUserId != currentUserId)
                {
                    _logger.LogWarning("EditAd POST: Kullanıcı {UserId}, kendisine ait olmayan ilanı ({ListingId}) güncellemeye çalıştı.", currentUserId, model.Id);
                    TempData["ErrorMessage"] = "Bu ilanı güncelleme yetkiniz yok.";
                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(myAds));
                }

                // Resim yükleme işlemi
                if (model.NewImage != null && model.NewImage.Length > 0)
                {
                    _logger.LogInformation("EditAd POST: Yeni resim yükleniyor. İlan ID: {ListingId}", model.Id);
                    string wwwRootPath = _webHostEnvironment.WebRootPath;
                    string imageDir = Path.Combine(wwwRootPath, "images", "listings");
                    if (!Directory.Exists(imageDir)) Directory.CreateDirectory(imageDir);

                    string extension = Path.GetExtension(model.NewImage.FileName);
                    string safeFileName = Path.GetRandomFileName().Replace(".", "") + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + extension;
                    newRelativeImagePath = "/" + Path.Combine("images", "listings", safeFileName).Replace('\\', '/');
                    newAbsoluteImagePath = Path.Combine(wwwRootPath, newRelativeImagePath.TrimStart('/'));

                    _logger.LogInformation("EditAd POST: Yeni resim kaydedilecek: {AbsolutePath}", newAbsoluteImagePath);
                    using (var fileStream = new FileStream(newAbsoluteImagePath, FileMode.Create))
                    {
                        await model.NewImage.CopyToAsync(fileStream);
                    }
                    _logger.LogInformation("EditAd POST: Yeni resim başarıyla kaydedildi: {RelativePath}", newRelativeImagePath);

                    // Eski resmi ve photos kaydını sil/güncelle
                    if (!string.IsNullOrEmpty(currentDbCoverImageUrl) && currentDbCoverImageUrl != "/images/default-ad-image.png")
                    {
                        oldImagePathToDelete = Path.Combine(wwwRootPath, currentDbCoverImageUrl.TrimStart('/'));
                        // photos tablosundan eski kaydı sil (veya is_cover = false yap)
                        string deleteOldPhotoSql = "DELETE FROM photos WHERE listing_id = @listing_id AND image_url = @old_image_url AND is_cover = TRUE";
                        using (NpgsqlCommand deletePhotoCmd = new NpgsqlCommand(deleteOldPhotoSql, connection, transaction))
                        {
                            deletePhotoCmd.Parameters.AddWithValue("@listing_id", model.Id);
                            deletePhotoCmd.Parameters.AddWithValue("@old_image_url", currentDbCoverImageUrl);
                            await deletePhotoCmd.ExecuteNonQueryAsync();
                            _logger.LogInformation("EditAd POST: Eski kapak resmi kaydı ({OldImageUrl}) photos tablosundan silindi. İlan ID: {ListingId}", currentDbCoverImageUrl, model.Id);
                        }
                    }
                    
                    // Yeni resmi photos tablosuna ekle
                    string insertNewPhotoSql = "INSERT INTO photos (listing_id, image_url, is_cover) VALUES (@listing_id, @image_url, TRUE)";
                    using (NpgsqlCommand insertPhotoCmd = new NpgsqlCommand(insertNewPhotoSql, connection, transaction))
                    {
                        insertPhotoCmd.Parameters.AddWithValue("@listing_id", model.Id);
                        insertPhotoCmd.Parameters.AddWithValue("@image_url", newRelativeImagePath);
                        await insertPhotoCmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("EditAd POST: Yeni kapak resmi kaydı ({NewImageUrl}) photos tablosuna eklendi. İlan ID: {ListingId}", newRelativeImagePath, model.Id);
                    }
                    model.CurrentCoverImageUrl = newRelativeImagePath; // Viewmodeldeki güncel URL'yi de set et.
                }
                else
                {
                    // Yeni resim yüklenmediyse mevcut resim URL'sini koru
                    newRelativeImagePath = currentDbCoverImageUrl;
                    model.CurrentCoverImageUrl = currentDbCoverImageUrl; // ViewModel'deki mevcut URL'yi de güncelle
                }

                // listings tablosunu güncelle
                string updateListingSql = @"
                    UPDATE listings 
                    SET title = @title, description = @description, location = @location, 
                        price_per_night = @price_per_night, is_active = @is_active
                    WHERE id = @id AND user_id = @user_id;
                ";
                _logger.LogInformation("EditAd POST: listings tablosu güncelleniyor. İlan ID: {ListingId}", model.Id);
                using (NpgsqlCommand updateCmd = new NpgsqlCommand(updateListingSql, connection, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@id", model.Id);
                    updateCmd.Parameters.AddWithValue("@user_id", currentUserId); // Ekstra güvenlik katmanı
                    updateCmd.Parameters.AddWithValue("@title", model.Title);
                    updateCmd.Parameters.AddWithValue("@description", (object)model.Description ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@location", model.Location);
                    updateCmd.Parameters.AddWithValue("@price_per_night", model.PricePerNight);
                    updateCmd.Parameters.AddWithValue("@is_active", model.IsActive);

                    int rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        _logger.LogWarning("EditAd POST: listings tablosunda hiçbir satır güncellenmedi (ya ilan bulunamadı ya da user_id eşleşmedi). İlan ID: {ListingId}, Kullanıcı ID: {UserId}", model.Id, currentUserId);
                        // Bu durum yukarıdaki ilk user_id kontrolünde yakalanmalıydı, ama bir güvenlik olarak kalabilir.
                        TempData["ErrorMessage"] = "İlan güncellenirken bir sorun oluştu veya ilan bulunamadı.";
                        await transaction.RollbackAsync();
                        // Yeni resim yüklendiyse ve kaydedildiyse sil
                        if (!string.IsNullOrEmpty(newAbsoluteImagePath) && System.IO.File.Exists(newAbsoluteImagePath) && (model.NewImage != null && model.NewImage.Length > 0))
                        {
                            System.IO.File.Delete(newAbsoluteImagePath);
                        }
                        return View(model);
                    }
                     _logger.LogInformation("EditAd POST: listings tablosu başarıyla güncellendi. İlan ID: {ListingId}", model.Id);
                }

                await transaction.CommitAsync();
                _logger.LogInformation("EditAd POST: Transaction başarıyla commit edildi. İlan ID: {ListingId}", model.Id);

                // Eski resim dosyasını diskten sil (transaction commit edildikten sonra)
                if (!string.IsNullOrEmpty(oldImagePathToDelete) && System.IO.File.Exists(oldImagePathToDelete))
                {
                    try
                    {
                        System.IO.File.Delete(oldImagePathToDelete);
                        _logger.LogInformation("EditAd POST: Eski kapak resmi dosyası ({OldImagePath}) diskten silindi. İlan ID: {ListingId}", oldImagePathToDelete, model.Id);
                    }
                    catch (IOException ioEx)
                    {
                        _logger.LogError(ioEx, "EditAd POST: Eski resim dosyası silinirken hata. Path: {OldImagePath}", oldImagePathToDelete);
                        // Bu kritik bir hata değil, sadece logla.
                    }
                }

                TempData["SuccessMessage"] = "İlan başarıyla güncellendi.";
                return RedirectToAction(nameof(myAds));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EditAd POST işlemi sırasında HATA oluştu. İlan ID: {ListingId}", model.Id);
            if (transaction != null && transaction.Connection != null && transaction.Connection.State == System.Data.ConnectionState.Open)
            {
                try { await transaction.RollbackAsync(); _logger.LogInformation("EditAd POST: Hata nedeniyle transaction rollback yapıldı."); }
                catch (Exception rbEx) { _logger.LogError(rbEx, "EditAd POST: Transaction rollback sırasında hata."); }
            }

            // Hata durumunda yüklenmiş olabilecek yeni resmi sil
            if (!string.IsNullOrEmpty(newAbsoluteImagePath) && System.IO.File.Exists(newAbsoluteImagePath) && (model.NewImage != null && model.NewImage.Length > 0) )
            {
                 try { System.IO.File.Delete(newAbsoluteImagePath); _logger.LogInformation("EditAd POST: Hata nedeniyle yüklenen yeni resim silindi: {ImagePath}", newAbsoluteImagePath); }
                 catch (Exception delEx) { _logger.LogError(delEx, "EditAd POST: Hata sonrası yeni resim silinirken hata: {ImagePath}", newAbsoluteImagePath); }
            }

            TempData["ErrorMessage"] = "İlan güncellenirken bir hata oluştu: " + ex.Message;
            // Hata olduğunda modeli view'a geri gönderirken CurrentCoverImageUrl'in doğru olduğundan emin ol.
            // Eğer yeni resim yüklenmeye çalışıldıysa ama hata oluştuysa, view'ın eski resmi göstermesi gerekir.
            // Ancak AdEditViewModel.CurrentCoverImageUrl zaten gizli alandan post ediliyor ve veritabanından gelen son haliyle güncelleniyor olmalı.
            // Yine de, emin olmak için, eğer model.NewImage varsa ve hata oluştuysa, eski CurrentCoverImageUrl'yi veritabanından tekrar çekebiliriz ya da view modelde tutarlı olmasını sağlarız.
            // Şimdilik, model olduğu gibi geri gönderiliyor. Formdaki hidden field sayesinde CurrentCoverImageUrl korunacaktır.
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdConfirmed(int id)
    {
        _logger.LogInformation("DeleteAdConfirmed POST çağrıldı. Silinecek İlan ID: {ListingId}", id);
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int currentUserId))
        {
            _logger.LogWarning("DeleteAdConfirmed POST: Kullanıcı kimliği alınamadı veya geçersiz.");
            TempData["ErrorMessage"] = "İşlem için oturum açmış olmanız gerekmektedir.";
            return RedirectToAction(nameof(myAds)); // veya Login
        }

        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        NpgsqlTransaction transaction = null;
        List<string> imagePathsToDelete = new List<string>();

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                transaction = await connection.BeginTransactionAsync();

                // İlanın kullanıcıya ait olup olmadığını ve var olup olmadığını kontrol et
                int dbUserId = 0;
                using (var checkCmd = new NpgsqlCommand("SELECT user_id FROM listings WHERE id = @id", connection, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@id", id);
                    var result = await checkCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        dbUserId = Convert.ToInt32(result);
                    }
                    else
                    {
                        _logger.LogWarning("DeleteAdConfirmed POST: Silinmek istenen ilan ({ListingId}) bulunamadı.", id);
                        TempData["ErrorMessage"] = "Silinmek istenen ilan bulunamadı.";
                        await transaction.RollbackAsync();
                        return RedirectToAction(nameof(myAds));
                    }
                }

                if (dbUserId != currentUserId)
                {
                    _logger.LogWarning("DeleteAdConfirmed POST: Kullanıcı {UserId}, kendisine ait olmayan ilanı ({ListingId}) silmeye çalıştı.", currentUserId, id);
                    TempData["ErrorMessage"] = "Bu ilanı silme yetkiniz yok.";
                    await transaction.RollbackAsync();
                    return RedirectToAction(nameof(myAds));
                }

                // İlişkili fotoğrafların yollarını al ve diskten silmek üzere listeye ekle
                using (var photoCmd = new NpgsqlCommand("SELECT image_url FROM photos WHERE listing_id = @listing_id", connection, transaction))
                {
                    photoCmd.Parameters.AddWithValue("@listing_id", id);
                    using (var reader = await photoCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                string relativeImagePath = reader.GetString(0);
                                if (!string.IsNullOrEmpty(relativeImagePath) && relativeImagePath != "/images/default-ad-image.png")
                                {
                                    imagePathsToDelete.Add(Path.Combine(_webHostEnvironment.WebRootPath, relativeImagePath.TrimStart('/')));
                                }
                            }
                        }
                    } // reader burada dispose edilecek
                }


                // Photos tablosundan kayıtları sil
                using (var deletePhotosCmd = new NpgsqlCommand("DELETE FROM photos WHERE listing_id = @listing_id", connection, transaction))
                {
                    deletePhotosCmd.Parameters.AddWithValue("@listing_id", id);
                    int photosDeletedCount = await deletePhotosCmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("DeleteAdConfirmed POST: {Count} fotoğraf kaydı silindi (İlan ID: {ListingId}).", photosDeletedCount, id);
                }

                // Listings tablosundan ilanı sil
                using (var deleteListingCmd = new NpgsqlCommand("DELETE FROM listings WHERE id = @id", connection, transaction))
                {
                    deleteListingCmd.Parameters.AddWithValue("@id", id);
                    int listingDeletedCount = await deleteListingCmd.ExecuteNonQueryAsync();
                    if (listingDeletedCount == 0)
                    {
                        // Bu durum yukarıdaki kontrolle zaten yakalanmış olmalı ama yine de bir güvenlik önlemi.
                        _logger.LogWarning("DeleteAdConfirmed POST: listings tablosunda silinecek kayıt bulunamadı. İlan ID: {ListingId}", id);
                        TempData["ErrorMessage"] = "İlan silinirken bir sorun oluştu veya ilan zaten silinmiş.";
                        await transaction.RollbackAsync();
                        return RedirectToAction(nameof(myAds));
                    }
                    _logger.LogInformation("DeleteAdConfirmed POST: İlan başarıyla listings tablosundan silindi. İlan ID: {ListingId}", id);
                }

                await transaction.CommitAsync();
                _logger.LogInformation("DeleteAdConfirmed POST: Transaction başarıyla commit edildi. İlan ID: {ListingId}", id);

                // Fiziksel resim dosyalarını diskten sil (transaction commit edildikten sonra)
                foreach (var imagePath in imagePathsToDelete)
                {
                    if (System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(imagePath);
                            _logger.LogInformation("DeleteAdConfirmed POST: Resim dosyası diskten silindi: {ImagePath}", imagePath);
                        }
                        catch (IOException ioEx)
                        {
                            _logger.LogError(ioEx, "DeleteAdConfirmed POST: Resim dosyası silinirken hata. Path: {ImagePath}", imagePath);
                            // Bu kritik bir hata değil, sadece logla. İlan veritabanından silindi.
                        }
                    }
                }

                TempData["SuccessMessage"] = "İlan başarıyla silindi.";
                return RedirectToAction(nameof(myAds));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAdConfirmed POST işlemi sırasında HATA oluştu. İlan ID: {ListingId}", id);
            if (transaction != null && transaction.Connection != null && transaction.Connection.State == System.Data.ConnectionState.Open)
            {
                try { await transaction.RollbackAsync(); _logger.LogInformation("DeleteAdConfirmed POST: Hata nedeniyle transaction rollback yapıldı."); }
                catch (Exception rbEx) { _logger.LogError(rbEx, "DeleteAdConfirmed POST: Transaction rollback sırasında hata."); }
            }
            TempData["ErrorMessage"] = "İlan silinirken bir hata oluştu: " + ex.Message;
            return RedirectToAction(nameof(myAds)); // Hata durumunda myAds'e veya EditAd sayfasına yönlendirilebilir.
        }
    }

    [Authorize] // Bu eyleme erişim için kullanıcının giriş yapmış olması gerekir
    [HttpGet] // GET isteği için olduğunu belirtelim
    public async Task<IActionResult> settings()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            _logger.LogWarning("Settings GET: Kullanıcı kimliği alınamadı veya geçersiz.");
            return RedirectToAction("Login", "Account");
        }

        string userName = null;
        string userEmail = User.FindFirstValue(ClaimTypes.Email); // E-postayı claim'den alabiliriz.

        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        try
        {
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT name FROM users WHERE id = @UserId";
                await using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    var result = await command.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        userName = result.ToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Settings GET: Kullanıcı adı çekilirken hata oluştu. UserId: {UserId}", userId);
            TempData["ErrorMessage"] = "Kullanıcı bilgileri yüklenirken bir hata oluştu.";
            // Hata durumunda boş modelle view döndürülebilir veya başka bir sayfaya yönlendirilebilir.
        }

        if (userName == null)
        {
            // Kullanıcı veritabanında bulunamazsa (teorik olarak olmamalı çünkü kimlik doğrulanmış)
            _logger.LogWarning("Settings GET: UserId {UserId} için veritabanında kullanıcı adı bulunamadı.", userId);
            TempData["ErrorMessage"] = "Kullanıcı bilgileri bulunamadı.";
            // return RedirectToAction("Index", "Home"); // Ana sayfaya yönlendirilebilir
        }

        var model = new UserSettingsViewModel
        {
            Name = userName
        };
        ViewBag.UserEmail = userEmail; // E-postayı ViewBag ile taşıyalım, formda readonly gösterilecek.

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSettings(UserSettingsViewModel model)
    {
        ViewBag.UserEmail = User.FindFirstValue(ClaimTypes.Email); // E-postayı tekrar ViewBag'e ekleyelim, sayfa yenilendiğinde lazım olacak.

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("UpdateSettings POST: ModelState geçerli değil.");
            TempData["ErrorMessage"] = "Lütfen gerekli alanları doğru şekilde doldurun.";
            return View("settings", model); // ViewBag.UserEmail burada view'a gitmeli
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            _logger.LogWarning("UpdateSettings POST: Kullanıcı kimliği alınamadı veya geçersiz.");
            TempData["ErrorMessage"] = "Oturumunuzla ilgili bir sorun oluştu. Lütfen tekrar giriş yapın.";
            return RedirectToAction("Login", "Account");
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
                        _logger.LogInformation("UpdateSettings POST: Kullanıcı {UserId} adı başarıyla güncellendi: {Name}", userId, model.Name);
                        TempData["SuccessMessage"] = "Adınız başarıyla güncellendi.";
                    }
                    else
                    {
                        _logger.LogWarning("UpdateSettings POST: Kullanıcı {UserId} için ad güncellenirken kayıt bulunamadı veya değişiklik olmadı.", userId);
                        TempData["ErrorMessage"] = "Bilgileriniz güncellenirken bir sorun oluştu veya değişiklik yapılmadı.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateSettings POST: Kullanıcı adı güncellenirken hata oluştu. UserId: {UserId}", userId);
            TempData["ErrorMessage"] = "Bilgileriniz güncellenirken bir veritabanı hatası oluştu.";
        }

        // Güncellenmiş modeli (veya aynı modeli) ve ViewBag.UserEmail'i view'a geri gönder.
        // Eğer işlem başarılıysa ve kullanıcıyı başka bir sayfaya yönlendirmek istemiyorsanız, 
        // güncel bilgileri tekrar yükleyip göndermek daha iyi olabilir.
        // Şimdilik aynı modeli geri gönderiyoruz, TempData mesajları sonucu gösterecek.
        return View("settings", model);
    }

    // HostController içindeki Login metodu, AccountController'daki ile çakışıyor.
    // Genellikle tek bir Login metodu (AccountController'da) olması tercih edilir.
    // Bu metodu şimdilik yorum satırına alıyorum veya kaldırabilirsiniz.
    /* 
    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        _logger.LogInformation("Login attempt for email: {Email}", email);
        try
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Kullanıcı adı ve şifre gereklidir");
                _logger.LogWarning("Login attempt failed: Email or password empty for email: {Email}", email);
                return View();
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            bool isValidUser = false;
            string dbPasswordHash = null;
            int userId = 0;
            string userRole = null; // Rolü de çekmek için

            string query = "SELECT id, password_hash, role FROM users WHERE LOWER(email) = LOWER(@Email)";
            _logger.LogInformation("Executing query to get user ID and password hash for email: {Email}. Query: {QueryText}", email, query);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                _logger.LogInformation("DB connection opened for login attempt for email: {Email}", email);
                await using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email.ToLower());
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

            _logger.LogInformation("User ID from DB: {UserId}. DB Password Hash retrieved for email: {Email}", userId, email);

            // GÜVENLİK UYARISI: Düz metin şifre karşılaştırması. BCrypt.Net.BCrypt.Verify kullanılmalı!
            if (userId > 0 && dbPasswordHash != null && dbPasswordHash == password) 
            {
                isValidUser = true;
            }

            _logger.LogInformation("isValidUser: {IsValidUser} for email: {Email} (User ID: {UserId})", isValidUser, email, userId);

            if (isValidUser)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, email), // Veya kullanıcı adı
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, userRole) // Rol claim'i
                };

                _logger.LogInformation("Attempting to sign in user. Email: {Email}, UserID: {UserId}. Claims to be set:", email, userId);
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
                
                // Role göre yönlendirme
                if (userRole == "host")
                {
                    return RedirectToAction("hostPage", "Host");
                }
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre");
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
    */
}