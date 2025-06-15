namespace realCabinly.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password_Hash { get; set; }
        public string Role { get; set; }
        public DateTime Created_At { get; set; }
    }

    public class AdminLoginViewModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class DashboardViewModel
    {
        public int UserCount { get; set; }
        public int ListingCount { get; set; }
        public int BookingCount { get; set; }
    }

    public class AdminListingViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public decimal Price_Per_Night { get; set; }
        public bool Is_Active { get; set; }
        public string OwnerName { get; set; } // From users table
    }
    
    public class AdminBookingViewModel
    {
        public int Id { get; set; }
        public string ListingTitle { get; set; } // From listings table
        public string UserName { get; set; } // From users table
        public DateTime Start_Date { get; set; }
        public DateTime End_Date { get; set; }
        public decimal Total_Price { get; set; }
        public string Status { get; set; }
    }

    public class Photo
    {
        public int Id { get; set; }
        public int Listing_Id { get; set; }
        public string Image_Url { get; set; }
        public bool Is_Cover { get; set; }
    }

    public class AdminListingDetailsViewModel : AdminListingViewModel
    {
        public string Description { get; set; }
        public List<Photo> Photos { get; set; }
    }

    public class PaymentViewModel
    {
        public int Id { get; set; }
        public int Booking_Id { get; set; }
        public string UserName { get; set; }
        public string ListingTitle { get; set; }
        public decimal Amount { get; set; }
        public string Payment_Method { get; set; }
        public string Status { get; set; }
        public DateTime Paid_At { get; set; }
    }

    public class FinancialReportViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int TotalPayments { get; set; }
        public int PendingPayments { get; set; }
        public int CompletedPayments { get; set; }
        public int FailedPayments { get; set; }
        public List<PaymentViewModel> RecentPayments { get; set; }
    }

    public class UserStatisticsViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int AdminCount { get; set; }
        public int HostCount { get; set; }
        public int GuestCount { get; set; }
        public List<MonthlyUserGrowth> UserGrowthData { get; set; }
    }

    public class MonthlyUserGrowth
    {
        public string Month { get; set; }
        public int UserCount { get; set; }
    }

    public class SystemOverviewViewModel
    {
        public int TotalListings { get; set; }
        public int ActiveListings { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public int ConfirmedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
    }
} 