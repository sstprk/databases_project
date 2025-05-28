using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace realCabinly.Models
{
    public class Bookings
    {

        public int id { get; set; }
        public int user_id { get; set; }

        public int listing_id { get; set; }

        public DateTime start_date { get; set; }

        public DateTime end_date { get; set; }

        public float total_price { get; set; }
        
        public string? status { get; set; } // pending, confirmed, cancelled
        
    }
}