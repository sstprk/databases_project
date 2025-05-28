using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace realCabinly.Models
{
    public class Users
    {
        
        public int id { get; set; }
        public string? name { get; set; }
        public string? email { get; set; }
        public string? password_hash { get; set; }
        public string? role { get; set; }
        
    }
}