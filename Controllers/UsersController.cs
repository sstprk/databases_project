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
    public class UsersController : Controller
    {
        private readonly ILogger<UsersController> _logger;
        public string connectionString = "Host=localhost;Username=postgres;Password=12;Database=database_project";
        public UsersController(ILogger<UsersController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult deneme()
        {
            //veri çekme mekanı
            List<Users> userlist = new List<Users>();
            NpgsqlConnection conn = new NpgsqlConnection("Host=localhost;Username=postgres;Password=12;Database=database_project");
            conn.Open();
            NpgsqlCommand cmd = new NpgsqlCommand();
            cmd.Connection = conn;
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = "SELECT * FROM users";
            NpgsqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                var uslist = new Users();
                uslist.id = Convert.ToInt32(dr["id"]);
                uslist.name = dr["name"].ToString();
                uslist.email = dr["email"].ToString();
                uslist.password_hash = dr["password_hash"].ToString();
                uslist.role = dr["role"].ToString();
                userlist.Add(uslist);
            }
            return View(userlist);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}