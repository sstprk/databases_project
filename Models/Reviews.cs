using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace realCabinly.Models;

public class Reviews
{
    public int id { get; set; }
    public int user_id { get; set; }
    public string? listing_id { get; set; }
    public int rating { get; set; } // Assuming rating is an integer
    public DateTime comment { get; set; } = DateTime.UtcNow;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}
