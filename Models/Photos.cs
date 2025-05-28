using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace realCabinly.Models;

public class Photos
{
    public int id { get; set; }
    public string? listing_id { get; set; } 
    public string? image_url { get; set; } 
    public Boolean is_cover { get; set; } 
}