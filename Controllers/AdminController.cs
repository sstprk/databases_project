using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Dapper;
using Npgsql;
using realCabinly.Models;

namespace realCabinly.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var user = await connection.QuerySingleOrDefaultAsync<User>(
                "SELECT * FROM users WHERE email = @Email", new { model.Email });

            if (user == null || model.Password != user.Password_Hash)
            {
                ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi.");
                return View(model);
            }

            if (user.Role.ToLower() != "admin")
            {
                ModelState.AddModelError(string.Empty, "Bu panele erişim yetkiniz yok.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true 
            };

            await HttpContext.SignInAsync("MyCookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Dashboard()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
            var listingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM listings");
            var bookingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings");

            var model = new DashboardViewModel
            {
                UserCount = userCount,
                ListingCount = listingCount,
                BookingCount = bookingCount
            };

            return View(model);
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Users()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var users = await connection.QueryAsync<User>("SELECT * FROM users ORDER BY id DESC");
            return View(users);
        }

        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var user = await connection.QuerySingleOrDefaultAsync<User>("SELECT * FROM users WHERE id = @Id", new { Id = id });

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                
                // Check if user exists
                var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                    "SELECT * FROM users WHERE id = @Id", new { Id = model.Id });
                
                if (existingUser == null)
                {
                    ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                    return View(model);
                }
                
                // Check if email is already taken by another user
                var emailExists = await connection.QuerySingleOrDefaultAsync<int>(
                    "SELECT COUNT(*) FROM users WHERE email = @Email AND id != @Id", 
                    new { Email = model.Email, Id = model.Id });
                
                if (emailExists > 0)
                {
                    ModelState.AddModelError("Email", "Bu e-posta adresi başka bir kullanıcı tarafından kullanılıyor.");
                    return View(model);
                }

                // Update user
                var rowsAffected = await connection.ExecuteAsync(
                    "UPDATE users SET name = @Name, email = @Email, role = @Role WHERE id = @Id",
                    new { model.Name, model.Email, model.Role, model.Id });

                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Kullanıcı başarıyla güncellendi.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Kullanıcı güncellenirken bir hata oluştu.";
                }

                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(model);
            }
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await connection.ExecuteAsync("DELETE FROM users WHERE id = @Id", new { Id = id });
            return RedirectToAction("Users");
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Listings()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var listings = await connection.QueryAsync<AdminListingViewModel>(
                @"SELECT l.id, l.title, l.location, l.price_per_night, l.is_active, u.name as OwnerName 
                  FROM listings l 
                  JOIN users u ON l.user_id = u.id 
                  ORDER BY l.id DESC");
            return View(listings);
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Bookings()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var bookings = await connection.QueryAsync<AdminBookingViewModel>(
                @"SELECT b.id, l.title as ListingTitle, u.name as UserName, b.start_date, b.end_date, b.total_price, b.status 
                  FROM bookings b 
                  JOIN listings l ON b.listing_id = l.id 
                  JOIN users u ON b.user_id = u.id 
                  ORDER BY b.id DESC");
            return View(bookings);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteListing(int id)
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            // The ON DELETE CASCADE constraint will handle related bookings, reviews, photos
            await connection.ExecuteAsync("DELETE FROM listings WHERE id = @Id", new { Id = id });
            return RedirectToAction("Listings");
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ListingDetails(int id)
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var listing = await connection.QuerySingleOrDefaultAsync<AdminListingDetailsViewModel>(
                @"SELECT l.id, l.title, l.description, l.location, l.price_per_night, l.is_active, u.name as OwnerName 
                  FROM listings l 
                  JOIN users u ON l.user_id = u.id 
                  WHERE l.id = @Id", new { Id = id });

            if (listing == null)
            {
                return NotFound();
            }

            listing.Photos = (await connection.QueryAsync<Photo>("SELECT * FROM photos WHERE listing_id = @Id", new { Id = id })).ToList();

            return View(listing);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int id)
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await connection.ExecuteAsync("UPDATE bookings SET status = 'cancelled' WHERE id = @Id", new { Id = id });
            return RedirectToAction("Bookings");
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> FinancialReports()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            
            var totalRevenue = await connection.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(amount), 0) FROM payments WHERE status = 'completed'");
            var monthlyRevenue = await connection.ExecuteScalarAsync<decimal>(
                "SELECT COALESCE(SUM(amount), 0) FROM payments WHERE status = 'completed' AND DATE_TRUNC('month', paid_at) = DATE_TRUNC('month', CURRENT_DATE)");
            
            var totalPayments = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM payments");
            var pendingPayments = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM payments WHERE status = 'pending'");
            var completedPayments = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM payments WHERE status = 'completed'");
            var failedPayments = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM payments WHERE status = 'failed'");

            var recentPayments = await connection.QueryAsync<realCabinly.Models.PaymentViewModel>(
                @"SELECT p.id, p.booking_id, u.name as UserName, l.title as ListingTitle, p.amount, p.payment_method, p.status, p.paid_at 
                  FROM payments p 
                  JOIN bookings b ON p.booking_id = b.id 
                  JOIN users u ON p.user_id = u.id 
                  JOIN listings l ON b.listing_id = l.id 
                  ORDER BY p.paid_at DESC LIMIT 10");

            var model = new FinancialReportViewModel
            {
                TotalRevenue = totalRevenue,
                MonthlyRevenue = monthlyRevenue,
                TotalPayments = totalPayments,
                PendingPayments = pendingPayments,
                CompletedPayments = completedPayments,
                FailedPayments = failedPayments,
                RecentPayments = recentPayments.ToList()
            };

            return View(model);
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UserStatistics()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            
            var totalUsers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
            var newUsersThisMonth = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM users WHERE DATE_TRUNC('month', created_at) = DATE_TRUNC('month', CURRENT_DATE)");
            
            var adminCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users WHERE role = 'admin'");
            var hostCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users WHERE role = 'host'");
            var guestCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users WHERE role = 'guest'");

            var model = new UserStatisticsViewModel
            {
                TotalUsers = totalUsers,
                ActiveUsers = totalUsers, // Assuming all users are active for now
                NewUsersThisMonth = newUsersThisMonth,
                AdminCount = adminCount,
                HostCount = hostCount,
                GuestCount = guestCount
            };

            return View(model);
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SystemOverview()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            
            var totalListings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM listings");
            var activeListings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM listings WHERE is_active = true");
            var totalBookings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings");
            var pendingBookings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings WHERE status = 'pending'");
            var confirmedBookings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings WHERE status = 'confirmed'");
            var cancelledBookings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM bookings WHERE status = 'cancelled'");
            var totalRevenue = await connection.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(amount), 0) FROM payments WHERE status = 'completed'");
            var monthlyRevenue = await connection.ExecuteScalarAsync<decimal>(
                "SELECT COALESCE(SUM(amount), 0) FROM payments WHERE status = 'completed' AND DATE_TRUNC('month', paid_at) = DATE_TRUNC('month', CURRENT_DATE)");

            var model = new SystemOverviewViewModel
            {
                TotalListings = totalListings,
                ActiveListings = activeListings,
                TotalBookings = totalBookings,
                PendingBookings = pendingBookings,
                ConfirmedBookings = confirmedBookings,
                CancelledBookings = cancelledBookings,
                TotalRevenue = totalRevenue,
                MonthlyRevenue = monthlyRevenue
            };

            return View(model);
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Payments()
        {
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var payments = await connection.QueryAsync<realCabinly.Models.PaymentViewModel>(
                @"SELECT p.id, p.booking_id, u.name as UserName, l.title as ListingTitle, p.amount, p.payment_method, p.status, p.paid_at 
                  FROM payments p 
                  JOIN bookings b ON p.booking_id = b.id 
                  JOIN users u ON p.user_id = u.id 
                  JOIN listings l ON b.listing_id = l.id 
                  ORDER BY p.paid_at DESC");
            return View(payments);
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("MyCookieAuth");
            return RedirectToAction("Index");
        }
    }
} 