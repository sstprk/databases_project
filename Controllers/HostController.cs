using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
// using System.Data.SqlClient; // SQL Server için
using Npgsql; // PostgreSQL için eklendi
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity; // Identity için eklendi

namespace realCabinly.Controllers;

public class HostController : Controller
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly UserManager<IdentityUser> _userManager; // UserManager eklendi

    public HostController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration, UserManager<IdentityUser> userManager) // UserManager enjekte edildi
    {
        _webHostEnvironment = webHostEnvironment;
        _configuration = configuration;
        _userManager = userManager; // Atama yapıldı
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

    public IActionResult myAds()
    {
        return View();
    }

    public IActionResult newAd()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAd(string adTitle, string adDescription, decimal adDailyPrice, IFormFile adImg, string location)
    {
        if (ModelState.IsValid && adImg != null && adImg.Length > 0 && !string.IsNullOrEmpty(location))
        {
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string fileName = Path.GetFileNameWithoutExtension(adImg.FileName);
            string extension = Path.GetExtension(adImg.FileName);
            string safeFileName = Path.GetRandomFileName().Replace(".", "") + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + extension;
            string relativeImagePath = Path.Combine("images", "ads", safeFileName);
            string absoluteImagePath = Path.Combine(wwwRootPath, relativeImagePath);

            var imageDirectory = Path.Combine(wwwRootPath, "images", "ads");
            if (!Directory.Exists(imageDirectory))
            {
                Directory.CreateDirectory(imageDirectory);
            }

            using (var fileStream = new FileStream(absoluteImagePath, FileMode.Create))
            {
                await adImg.CopyToAsync(fileStream);
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                // using (SqlConnection connection = new SqlConnection(connectionString)) // SQL Server için
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString)) // PostgreSQL için değiştirildi
                {
                    await connection.OpenAsync();
                    int userId = 1;

                    string sql = "INSERT INTO listings (user_id, title, description, location, price_per_night, is_active, created_at, image_path) VALUES (@user_id, @title, @description, @location, @price_per_night, @is_active, @created_at, @image_path)";
                    // using (SqlCommand command = new SqlCommand(sql, connection)) // SQL Server için
                    using (NpgsqlCommand command = new NpgsqlCommand(sql, connection)) // PostgreSQL için değiştirildi
                    {
                        command.Parameters.AddWithValue("@user_id", userId);
                        command.Parameters.AddWithValue("@title", adTitle);
                        command.Parameters.AddWithValue("@description", adDescription);
                        command.Parameters.AddWithValue("@location", location);
                        command.Parameters.AddWithValue("@price_per_night", adDailyPrice);
                        command.Parameters.AddWithValue("@is_active", true);
                        command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@image_path", "/" + relativeImagePath.Replace("\\\\", "/"));

                        await command.ExecuteNonQueryAsync();
                    }
                }
                return RedirectToAction(nameof(myAds));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "İlan oluşturulurken bir hata oluştu: " + ex.Message);
                if (System.IO.File.Exists(absoluteImagePath))
                {
                     System.IO.File.Delete(absoluteImagePath);
                }
            }
        }

        if (adImg == null || adImg.Length == 0)
        {
            ModelState.AddModelError("adImg", "Lütfen bir ilan görseli seçin.");
        }
        if (string.IsNullOrEmpty(location))
        {
            ModelState.AddModelError("location", "Lütfen bir konum girin.");
        }
        return View("newAd");
    }

    public IActionResult settings()
    {
        return View();
    }
}