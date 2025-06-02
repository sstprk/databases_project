using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using realCabinly.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}