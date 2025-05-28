using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using realCabinly.Models;


namespace realCabinly.Controllers
{
    [Route("[controller]")]
    public class BookingsController : Controller
    {
        private readonly ILogger<BookingsController> _logger;
        public string connectionString = "Host=localhost;Username=postgres;Password=12;Database=database_project";

        public BookingsController(ILogger<BookingsController> logger)
        {
            _logger = logger;
        }

        
        public IActionResult Index()
        {
            //veri çekme mekanı
            List<Bookings> bookinglist = new List<Bookings>();
            NpgsqlConnection conn = new NpgsqlConnection("Host=localhost;Username=postgres;Password=12;Database=database_project");
            conn.Open();
            NpgsqlCommand cmd = new NpgsqlCommand();
            cmd.Connection = conn;
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT * FROM bookings";
            NpgsqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                var booklist = new Bookings();
                booklist.id = Convert.ToInt32(dr["id"]);
                booklist.user_id = Convert.ToInt32(dr["user_id"]);
                booklist.listing_id = Convert.ToInt32(dr["listing_id"]);
                booklist.start_date = Convert.ToDateTime(dr["start_date"]);
                booklist.end_date = Convert.ToDateTime(dr["end_date"]);
                booklist.total_price = Convert.ToSingle(dr["total_price"]);
                booklist.status = dr["status"].ToString();
                bookinglist.Add(booklist);
            }
            return View(bookinglist);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}