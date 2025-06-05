using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using realCabinly.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Linq; // LINQ metodları için eklendi (örn: Select)
using System.Security.Claims;

namespace realCabinly.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Contact()
    {
        
        return View();
    }

    public IActionResult Login()
    {
        return View();
    }

    public async Task<IActionResult> Ads()
    {
        var ilanlar = new List<IlanView>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        string query = @"
            SELECT 
                l.id AS listing_id, 
                l.user_id, 
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
                l.is_active = TRUE 
            ORDER BY 
                l.created_at DESC;
        ";

        try
        {
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                _logger.LogInformation("Ads: Çalıştırılacak SQL: {SQLQuery}", query.Replace("\n", " ").Replace("\r", " "));
                await using (var command = new NpgsqlCommand(query, connection))
                {
                    await using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ilanlar.Add(new IlanView
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("listing_id")),
                                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                Location = reader.IsDBNull(reader.GetOrdinal("location")) ? string.Empty : reader.GetString(reader.GetOrdinal("location")),
                                PricePerNight = reader.GetDecimal(reader.GetOrdinal("price_per_night")),
                                IsAvailable = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                ImageUrl = reader.IsDBNull(reader.GetOrdinal("cover_image_url")) 
                                           ? "/images/default-ad-image.png" 
                                           : reader.GetString(reader.GetOrdinal("cover_image_url"))
                            });
                        }
                        _logger.LogInformation("Ads: {AdCount} adet ilan bulundu ve modellendi.", ilanlar.Count);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ads sayfasında ilanlar çekilirken hata oluştu.");
            // Hata durumunda boş bir liste veya özel bir hata mesajı ile view döndürülebilir.
            // Örneğin: ViewBag.ErrorMessage = "İlanlar yüklenirken bir sorun oluştu.";
        }
        return View(ilanlar);
    }

    // YENİ EKLENEN METOD: İlan Detay Sayfası
    public async Task<IActionResult> AdDetail(int id)
    {
        _logger.LogInformation("AdDetail sayfası için ilan ID: {IlanId} detayları çekiliyor.", id);
        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        IlanDetailViewModel ilanDetail = null;

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // 1. Temel ilan bilgilerini çek
                string listingSql = @"
                    SELECT 
                        l.id AS listing_id, 
                        l.user_id, 
                        l.title, 
                        l.description, 
                        l.location, 
                        l.price_per_night, 
                        l.is_active, 
                        l.created_at
                    FROM 
                        listings l
                    WHERE 
                        l.id = @listing_id;
                ";
                _logger.LogInformation("AdDetail: Çalıştırılacak SQL (listing): {SQLQuery}", listingSql.Replace("\n", " ").Replace("\r", " "));
                using (NpgsqlCommand command = new NpgsqlCommand(listingSql, connection))
                {
                    command.Parameters.AddWithValue("@listing_id", id);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            ilanDetail = new IlanDetailViewModel
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("listing_id")),
                                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                Location = reader.IsDBNull(reader.GetOrdinal("location")) ? string.Empty : reader.GetString(reader.GetOrdinal("location")),
                                PricePerNight = reader.GetDecimal(reader.GetOrdinal("price_per_night")),
                                IsAvailable = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                            };
                        }
                    }
                }

                if (ilanDetail == null)
                {
                    _logger.LogWarning("AdDetail: İlan ID {IlanId} için temel bilgiler bulunamadı.", id);
                    return NotFound(); // Veya özel bir "İlan Bulunamadı" view'ı
                }

                // 2. İlanın tüm resimlerini çek
                string photosSql = @"
                    SELECT image_url 
                    FROM photos 
                    WHERE listing_id = @listing_id 
                    ORDER BY is_cover DESC, id ASC; -- Kapak resmi başta, sonra diğerleri sırayla
                "; 
                _logger.LogInformation("AdDetail: Çalıştırılacak SQL (photos): {SQLQuery}", photosSql.Replace("\n", " ").Replace("\r", " "));
                using (NpgsqlCommand photoCommand = new NpgsqlCommand(photosSql, connection))
                {
                    photoCommand.Parameters.AddWithValue("@listing_id", id);
                    using (NpgsqlDataReader photoReader = await photoCommand.ExecuteReaderAsync())
                    {
                        while (await photoReader.ReadAsync())
                        {
                            string imageUrl = photoReader.IsDBNull(photoReader.GetOrdinal("image_url")) 
                                              ? "/images/default-ad-image.png" 
                                              : photoReader.GetString(photoReader.GetOrdinal("image_url"));
                            ilanDetail.AllImageUrls.Add(imageUrl);
                        }
                    }
                }
                _logger.LogInformation("AdDetail: İlan ID {IlanId} için {ImageCount} adet resim bulundu.", id, ilanDetail.AllImageUrls.Count);
                 // Eğer hiç resim bulunamazsa varsayılan bir resim eklenebilir veya kapak resmi (eğer ayrı bir alanda tutuluyorsa) başa eklenebilir.
                if (!ilanDetail.AllImageUrls.Any())
                {
                    ilanDetail.AllImageUrls.Add("/images/default-ad-image.png");
                    _logger.LogInformation("AdDetail: İlan ID {IlanId} için hiç resim bulunamadığından varsayılan resim eklendi.", id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdDetail sayfasında ilan ID {IlanId} için detaylar çekilirken hata oluştu.", id);
            // Hata durumunda kullanıcıya bir hata mesajı gösterilebilir.
            // Örneğin: TempData["ErrorMessage"] = "İlan detayları yüklenirken bir sorun oluştu.";
            // return RedirectToAction("Ads"); // Veya bir hata sayfasına yönlendir
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }); // Genel hata sayfası
        }

        return View(ilanDetail); // Views/Home/AdDetail.cshtml view'ına modeli gönder
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBooking(int listingId, DateTime checkInDate, DateTime checkOutDate, int guestCount)
    {
        _logger.LogInformation("CreateBooking POST action started for ListingId: {ListingId}, CheckIn: {CheckInDate}, CheckOut: {CheckOutDate}, GuestCount: {GuestCount}", listingId, checkInDate, checkOutDate, guestCount);

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            _logger.LogWarning("CreateBooking: User ID could not be parsed or was null/empty. Redirecting.");
            TempData["ReservationMessage"] = "Kullanıcı kimliği doğrulanamadı. Lütfen giriş yapın.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }
        _logger.LogInformation("CreateBooking: User ID {UserId} successfully parsed.", userId);

        if (checkInDate >= checkOutDate)
        {
            _logger.LogWarning("CreateBooking: Check-out date must be after check-in date. ListingId: {ListingId}", listingId);
            TempData["ReservationMessage"] = "Çıkış tarihi, giriş tarihinden sonra olmalıdır.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }

        if (checkInDate < DateTime.Today)
        {
            _logger.LogWarning("CreateBooking: Check-in date cannot be in the past. ListingId: {ListingId}", listingId);
            TempData["ReservationMessage"] = "Giriş tarihi geçmiş bir tarih olamaz.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }
        if (guestCount <= 0)
        {
            _logger.LogWarning("CreateBooking: Guest count must be positive. ListingId: {ListingId}, GuestCount: {GuestCount}", listingId, guestCount);
            TempData["ReservationMessage"] = "Misafir sayısı pozitif bir değer olmalıdır.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }

        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        decimal pricePerNight = 0m;
        decimal totalPrice = 0m;

        try
        {
            _logger.LogInformation("CreateBooking: Attempting to open DB connection for ListingId: {ListingId}", listingId);
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                _logger.LogInformation("CreateBooking: DB connection opened for ListingId: {ListingId}", listingId);

                string getPriceSql = "SELECT price_per_night FROM listings WHERE id = @listing_id;";
                using (NpgsqlCommand getPriceCommand = new NpgsqlCommand(getPriceSql, connection))
                {
                    getPriceCommand.Parameters.AddWithValue("@listing_id", listingId);
                    object resultPrice = await getPriceCommand.ExecuteScalarAsync();
                    if (resultPrice == null || resultPrice == DBNull.Value)
                    {
                        _logger.LogError("CreateBooking: Could not retrieve price_per_night for ListingId: {ListingId}.", listingId);
                        TempData["ReservationMessage"] = "İlan fiyat bilgisi alınamadı.";
                        return RedirectToAction("AdDetail", new { id = listingId });
                    }
                    pricePerNight = Convert.ToDecimal(resultPrice);
                    _logger.LogInformation("CreateBooking: Price per night for ListingId {ListingId} is {PricePerNight}.", listingId, pricePerNight);
                }

                if (pricePerNight <= 0)
                {
                     _logger.LogError("CreateBooking: Price per night is not valid (<=0) for ListingId: {ListingId}. Price: {PricePerNight}", listingId, pricePerNight);
                    TempData["ReservationMessage"] = "İlan fiyatı geçersiz.";
                    return RedirectToAction("AdDetail", new { id = listingId });
                }

                int numberOfNights = (checkOutDate - checkInDate).Days;
                if (numberOfNights <= 0) 
                {
                    _logger.LogWarning("CreateBooking: Number of nights is not positive. ListingId: {ListingId}, Nights: {NumberOfNights}", listingId, numberOfNights);
                    TempData["ReservationMessage"] = "Geçerli bir gece sayısı hesaplanamadı.";
                    return RedirectToAction("AdDetail", new { id = listingId });
                }
                totalPrice = numberOfNights * pricePerNight;
                _logger.LogInformation("CreateBooking: Calculated total price for {NumberOfNights} nights is {TotalPrice}. ListingId: {ListingId}", numberOfNights, totalPrice, listingId);
                
                string sql = "INSERT INTO bookings (user_id, listing_id, start_date, end_date, total_price, status) VALUES (@user_id, @listing_id, @start_date, @end_date, @total_price, 'pending') RETURNING id;";
                _logger.LogInformation("CreateBooking: SQL to execute for booking insertion: {SQL}", sql);
                using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    command.Parameters.AddWithValue("@listing_id", listingId);
                    command.Parameters.AddWithValue("@start_date", checkInDate);
                    command.Parameters.AddWithValue("@end_date", checkOutDate);
                    command.Parameters.AddWithValue("@total_price", totalPrice);

                    _logger.LogInformation("CreateBooking: Executing scalar command for booking insertion. ListingId: {ListingId}", listingId);
                    object result = await command.ExecuteScalarAsync();
                    
                    if (result == null || result == DBNull.Value)
                    {
                        _logger.LogError("CreateBooking: ExecuteScalarAsync returned null or DBNull for booking insertion. ListingId: {ListingId}.", listingId);
                        TempData["ReservationMessage"] = "Rezervasyon oluşturulamadı (veri tabanından ID alınamadı).";
                        return RedirectToAction("AdDetail", new { id = listingId });
                    }

                    int bookingId = Convert.ToInt32(result);
                    _logger.LogInformation("CreateBooking: Booking successfully created with ID: {BookingId} for ListingId: {ListingId}", bookingId, listingId);
                    
                    TempData["ReservationMessage"] = "Rezervasyon isteğiniz başarıyla alındı. Toplam Tutar: " + totalPrice.ToString("C");
                    return RedirectToAction("AdDetail", new { id = listingId });
                }
            }
        }
        catch (NpgsqlException pgEx)
        {
            _logger.LogError(pgEx, "CreateBooking NpgsqlException for ListingId: {ListingId}. Error: {ErrorMessage}. SQLState: {SQLState}. Details: {ErrorDetails}", listingId, pgEx.Message, pgEx.SqlState, pgEx.ToString());
            TempData["ReservationMessage"] = $"Veritabanı hatası oluştu: {pgEx.Message}";
            return RedirectToAction("AdDetail", new { id = listingId });
        }
        catch (InvalidCastException icEx)
        {
            _logger.LogError(icEx, "CreateBooking InvalidCastException for ListingId: {ListingId}. Could not convert result.", listingId);
            TempData["ReservationMessage"] = "Veri dönüştürme hatası oluştu.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateBooking Generic Exception for ListingId: {ListingId}. Exception Type: {ExceptionType}, Message: {ErrorMessage}. Details: {ErrorDetails}", listingId, ex.GetType().FullName, ex.Message, ex.ToString());
            TempData["ReservationMessage"] = "Rezervasyon sırasında genel bir hata oluştu.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}