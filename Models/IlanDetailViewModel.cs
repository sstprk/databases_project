using System;
using System.Collections.Generic;

namespace realCabinly.Models
{
    public class IlanDetailViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; } // İlan sahibinin ID'si (gerekirse)
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public decimal PricePerNight { get; set; }
        public bool IsAvailable { get; set; } // Veritabanındaki is_active durumu
        public DateTime CreatedAt { get; set; }
        public List<string> AllImageUrls { get; set; } // İlanın tüm resimleri
        // Olanaklar, yorumlar, ev sahibi bilgisi gibi ek özellikler buraya eklenebilir.
        // public List<string> Amenities { get; set; }
        // public string HostName { get; set; }

        public IlanDetailViewModel()
        {
            AllImageUrls = new List<string>();
            // Amenities = new List<string>();
        }
    }
} 