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
using System.Net.Mail;
using System.Net;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;

namespace realCabinly.Controllers;

public class PaymentViewModel
{
    public int BookingId { get; set; }
    public string ListingTitle { get; set; }
    public string Location { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public float TotalPrice { get; set; }
}

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

    [HttpGet]
    public IActionResult Search(string query, int? guestCount, DateTime? checkInDate, DateTime? checkOutDate)
    {
        return RedirectToAction("Ads", new { query, guestCount, checkInDate, checkOutDate });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string name, string email, string subject, string message)
    {
        try
        {
            // E-posta gönderme işlemi
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("Cabinly İletişim Formu", "cabinlyauth@gmail.com"));
            emailMessage.To.Add(new MailboxAddress("Cabinly", "cabinlyauth@gmail.com"));
            emailMessage.Subject = $"İletişim Formu: {subject}";
            emailMessage.Body = new TextPart("plain")
            {
                Text = $"Ad Soyad: {name}\n" +
                      $"E-posta: {email}\n" +
                      $"Konu: {subject}\n" +
                      $"Mesaj: {message}"
            };

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync("cabinlyauth@gmail.com", "ouoy kawo thtu cfpr");
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }

            TempData["SuccessMessage"] = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız.";
            return RedirectToAction("Contact");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İletişim formu gönderilirken hata oluştu.");
            TempData["ErrorMessage"] = "Mesajınız gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
            return RedirectToAction("Contact");
        }
    }

    public IActionResult Login()
    {
        return View();
    }

    public async Task<IActionResult> Ads(string query, int? guestCount, DateTime? checkInDate, DateTime? checkOutDate)
    {
        _logger.LogInformation("Ads sayfası için ilanlar çekiliyor.");
        List<IlanView> ilanlar = new List<IlanView>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        ViewBag.Query = query;
        ViewBag.GuestCount = guestCount;
        ViewBag.CheckInDate = checkInDate;
        ViewBag.CheckOutDate = checkOutDate;

        try
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                var sqlBuilder = new System.Text.StringBuilder(@"
                    SELECT 
                        l.id AS listing_id, l.user_id, l.title, l.description, l.location, 
                        l.price_per_night, l.is_active, l.created_at,
                        (SELECT p.image_url FROM photos p WHERE p.listing_id = l.id AND p.is_cover = TRUE LIMIT 1) AS cover_image_url
                    FROM listings l
                ");
                
                var whereClauses = new List<string>();
                var parameters = new List<Npgsql.NpgsqlParameter>();

                // Always filter for active listings
                whereClauses.Add("l.is_active = TRUE");

                if (!string.IsNullOrWhiteSpace(query))
                {
                    whereClauses.Add("(l.location ILIKE @query OR l.title ILIKE @query)");
                    parameters.Add(new Npgsql.NpgsqlParameter("query", $"%{query}%"));
                }

                if (checkInDate.HasValue && checkOutDate.HasValue && checkOutDate.Value > checkInDate.Value)
                {
                    whereClauses.Add(@"
                        l.id NOT IN (
                            SELECT b.listing_id FROM bookings b 
                            WHERE b.status != 'cancelled' AND (b.start_date, b.end_date) OVERLAPS (@checkInDate, @checkOutDate)
                        )
                    ");
                    parameters.Add(new Npgsql.NpgsqlParameter("checkInDate", checkInDate.Value.Date));
                    parameters.Add(new Npgsql.NpgsqlParameter("checkOutDate", checkOutDate.Value.Date));
                }

                if (whereClauses.Any())
                {
                    sqlBuilder.Append(" WHERE ").Append(string.Join(" AND ", whereClauses));
                }

                sqlBuilder.Append(" ORDER BY l.created_at DESC;");

                string finalSql = sqlBuilder.ToString();
                _logger.LogInformation("Ads: Çalıştırılacak SQL: {SQLQuery}", finalSql.Replace("\n", " ").Replace("\r", " "));

                await using (var command = new NpgsqlCommand(finalSql, connection))
                {
                    if (parameters.Any())
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                    }
                    
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
            ViewBag.ErrorMessage = "İlanlar yüklenirken bir sorun oluştu.";
        }
        return View(ilanlar);
    }

    [Authorize]
    public async Task<IActionResult> MyBookings()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            return RedirectToAction("Login", "Account");
        }

        List<Bookings> bookinglist = new List<Bookings>();
        Dictionary<int, string> paymentStatusDict = new Dictionary<int, string>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        try
        {
            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var cmd = new NpgsqlCommand(@"
                    SELECT 
                        b.id, b.user_id, b.listing_id, b.start_date, b.end_date, b.total_price, b.status,
                        p.status as payment_status
                    FROM bookings b
                    LEFT JOIN payments p ON b.id = p.booking_id
                    WHERE b.user_id = @UserId
                    ORDER BY b.start_date DESC", conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                await using (var dr = await cmd.ExecuteReaderAsync())
                {
                    while (await dr.ReadAsync())
                    {
                        var booking = new Bookings
                        {
                            id = Convert.ToInt32(dr["id"]),
                            user_id = Convert.ToInt32(dr["user_id"]),
                            listing_id = Convert.ToInt32(dr["listing_id"]),
                            start_date = Convert.ToDateTime(dr["start_date"]),
                            end_date = Convert.ToDateTime(dr["end_date"]),
                            total_price = Convert.ToSingle(dr["total_price"]),
                            status = dr["status"].ToString()
                        };
                        bookinglist.Add(booking);

                        var paymentStatus = dr.IsDBNull(dr.GetOrdinal("payment_status")) ? "pending" : dr["payment_status"].ToString();
                        paymentStatusDict.Add(booking.id, paymentStatus);
                    }
                }
            }
            ViewBag.PaymentStatuses = paymentStatusDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rezervasyonlarımı çekerken bir hata oluştu. UserId: {UserId}", userId);
            ViewBag.ErrorMessage = "Rezervasyonlarınız yüklenirken bir sorun oluştu.";
        }
        
        return View(bookinglist);
    }

    [Authorize]
    public async Task<IActionResult> Payment(int bookingId)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        PaymentViewModel paymentViewModel = null;

        try
        {
            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT 
                        b.id as booking_id, 
                        b.start_date, 
                        b.end_date, 
                        b.total_price,
                        l.title,
                        l.location
                    FROM bookings b
                    JOIN listings l ON b.listing_id = l.id
                    WHERE b.id = @BookingId";

                await using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@BookingId", bookingId);
                    await using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            paymentViewModel = new PaymentViewModel
                            {
                                BookingId = reader.GetInt32(reader.GetOrdinal("booking_id")),
                                ListingTitle = reader.GetString(reader.GetOrdinal("title")),
                                Location = reader.GetString(reader.GetOrdinal("location")),
                                CheckInDate = reader.GetDateTime(reader.GetOrdinal("start_date")),
                                CheckOutDate = reader.GetDateTime(reader.GetOrdinal("end_date")),
                                TotalPrice = reader.GetFloat(reader.GetOrdinal("total_price"))
                            };
                        }
                    }
                }
            }

            if (paymentViewModel == null)
            {
                TempData["ErrorMessage"] = "Ödeme için geçerli bir rezervasyon bulunamadı.";
                return RedirectToAction("MyBookings");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme sayfası yüklenirken hata oluştu. BookingId: {BookingId}", bookingId);
            TempData["ErrorMessage"] = "Ödeme sayfası yüklenirken bir hata oluştu. Lütfen tekrar deneyin.";
            return RedirectToAction("MyBookings");
        }

        return View(paymentViewModel);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment(int bookingId)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        try
        {
            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = @"UPDATE payments 
                                 SET status = @Status, 
                                     payment_method = @PaymentMethod, 
                                     transaction_id = @TransactionId, 
                                     paid_at = CURRENT_TIMESTAMP 
                                 WHERE booking_id = @BookingId AND status = 'pending'";

                await using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Status", "completed");
                    cmd.Parameters.AddWithValue("@PaymentMethod", "Credit Card");
                    cmd.Parameters.AddWithValue("@TransactionId", Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue("@BookingId", bookingId);
                    int updatedRows = await cmd.ExecuteNonQueryAsync();

                    if (updatedRows > 0)
                    {
                        TempData["SuccessMessage"] = "Ödemeniz başarıyla tamamlandı!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Ödeme işlenemedi. Rezervasyon zaten ödenmiş olabilir veya bir hata oluştu.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ödeme işlenirken hata oluştu. BookingId: {BookingId}", bookingId);
            TempData["ErrorMessage"] = "Ödeme sırasında beklenmedik bir hata oluştu.";
        }

        return RedirectToAction("MyBookings");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(int listingId, int rating, string comment)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            TempData["ReviewError"] = "Yorum yapabilmek için giriş yapmalısınız.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }

        if (rating < 1 || rating > 5 || string.IsNullOrWhiteSpace(comment))
        {
            TempData["ReviewError"] = "Lütfen 1 ile 5 arasında bir puan seçin ve yorumunuzu yazın.";
            return RedirectToAction("AdDetail", new { id = listingId });
        }

        string connectionString = _configuration.GetConnectionString("DefaultConnection");
        try
        {
            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // Kullanıcının bu ilanda konaklayıp konaklamadığını kontrol et
                string checkBookingSql = @"
                    SELECT 1 FROM bookings 
                    WHERE user_id = @UserId 
                    AND listing_id = @ListingId 
                    AND status = 'confirmed' 
                    AND end_date < CURRENT_DATE
                    LIMIT 1;";
                
                await using (var checkCmd = new NpgsqlCommand(checkBookingSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@UserId", userId);
                    checkCmd.Parameters.AddWithValue("@ListingId", listingId);
                    var canReview = await checkCmd.ExecuteScalarAsync();

                    if (canReview == null)
                    {
                        TempData["ReviewError"] = "Yalnızca tamamlanmış bir konaklamanız olan ilanlara yorum yapabilirsiniz.";
                        return RedirectToAction("AdDetail", new { id = listingId });
                    }
                }

                // Yorumu ekle
                string insertReviewSql = @"
                    INSERT INTO reviews (user_id, listing_id, rating, comment) 
                    VALUES (@UserId, @ListingId, @Rating, @Comment)";

                await using (var insertCmd = new NpgsqlCommand(insertReviewSql, conn))
                {
                    insertCmd.Parameters.AddWithValue("@UserId", userId);
                    insertCmd.Parameters.AddWithValue("@ListingId", listingId);
                    insertCmd.Parameters.AddWithValue("@Rating", rating);
                    insertCmd.Parameters.AddWithValue("@Comment", comment);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                TempData["ReviewSuccess"] = "Yorumunuz başarıyla eklendi!";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yorum eklenirken hata oluştu. ListingId: {ListingId}, UserId: {UserId}", listingId, userId);
            TempData["ReviewError"] = "Yorumunuz eklenirken beklenmedik bir hata oluştu.";
        }

        return RedirectToAction("AdDetail", new { id = listingId });
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

                // 3. İlanın yorumlarını çek
                string reviewsSql = @"
                    SELECT r.rating, r.comment, r.created_at, u.name as user_name
                    FROM reviews r
                    JOIN users u ON r.user_id = u.id
                    WHERE r.listing_id = @listing_id
                    ORDER BY r.created_at DESC;
                ";
                _logger.LogInformation("AdDetail: Çalıştırılacak SQL (reviews): {SQLQuery}", reviewsSql.Replace("\n", " ").Replace("\r", " "));
                using (NpgsqlCommand reviewCommand = new NpgsqlCommand(reviewsSql, connection))
                {
                    reviewCommand.Parameters.AddWithValue("@listing_id", id);
                    using (NpgsqlDataReader reviewReader = await reviewCommand.ExecuteReaderAsync())
                    {
                        while (await reviewReader.ReadAsync())
                        {
                            ilanDetail.Reviews.Add(new ReviewViewModel
                            {
                                UserName = reviewReader.GetString(reviewReader.GetOrdinal("user_name")),
                                Rating = reviewReader.GetInt32(reviewReader.GetOrdinal("rating")),
                                Comment = reviewReader.GetString(reviewReader.GetOrdinal("comment")),
                                CreatedAt = reviewReader.GetDateTime(reviewReader.GetOrdinal("created_at"))
                            });
                        }
                    }
                }
                _logger.LogInformation("AdDetail: İlan ID {IlanId} için {ReviewCount} adet yorum bulundu.", id, ilanDetail.Reviews.Count);
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

    public IActionResult AccessDenied()
    {
        return View();
    }
}