namespace Aerion.Pages;

public class BookModel : AppPage
{
    private readonly BookingService _svc;

    public BookModel(Db db, BookingService svc) : base(db)
    {
        _svc = svc;
    }

    protected override string[] AllowedRoles => new[] { "passenger", "staff" };

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string Seats { get; set; } = "";
    [BindProperty] public string Method { get; set; } = "card";
    [BindProperty] public int MilesUnits { get; set; }
    [BindProperty] public string? PassengerUsername { get; set; }

    public string FlightNumber { get; set; } = "";
    public string RouteText { get; set; } = "";
    public DateTime Dep { get; set; }
    public string Model { get; set; } = "";
    public decimal Economy { get; set; }
    public decimal Business { get; set; }
    public List<Cabin> Cabins { get; set; } = new();
    public List<int> ExitRows { get; set; } = new();
    public HashSet<string> Taken { get; set; } = new();
    public int Miles { get; set; }
    public int MaxUnits { get; set; }
    public bool IsStaff => Role == "staff";

    public IActionResult OnGet()
    {
        return Load() ? Page() : RedirectToPage("/Search");
    }

    public IActionResult OnPost()
    {
        var seats = Seats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        int? passengerId = PassengerId;
        if (IsStaff)
        {
            // Counter booking: the staff member books on behalf of an existing passenger.
            passengerId = Db.Query("SELECT p.PassengerID FROM Passenger p JOIN UserAccount u ON u.UserID = p.UserID WHERE u.Username = @u",
                r => (int?)r.Int("PassengerID"), ("@u", PassengerUsername ?? "")).FirstOrDefault();
            if (passengerId == null)
            {
                Err("Enter the username of an existing passenger.");
                return RedirectToPage(new { Id });
            }
        }
        var result = _svc.Book(Id, passengerId!.Value, seats, IsStaff ? "cash" : Method, IsStaff ? 0 : MilesUnits, EmployeeId);
        if (!result.Ok)
        {
            Err(result.Message);
            return RedirectToPage(new { Id });
        }
        Ok(result.Message);
        return RedirectToPage(IsStaff ? "/Staff/Index" : "/MyBookings");
    }

    private bool Load()
    {
        var f = Db.One(
            "SELECT f.FlightNumber, f.DepartureTime, f.Status, ac.Model, ac.TotalSeats, r.BaseFare, a.Code AS FromCode, b.Code AS ToCode " +
            "FROM Flight f JOIN Route r ON r.RouteID = f.RouteID JOIN Airport a ON a.AirportID = r.DepartureAirportID " +
            "JOIN Airport b ON b.AirportID = r.ArrivalAirportID JOIN Aircraft ac ON ac.AircraftID = f.AircraftID WHERE f.FlightID = @f",
            r => new { Num = r.Str("FlightNumber"), Dep = r.Date("DepartureTime"), St = r.Str("Status"), M = r.Str("Model"), Seats = r.Int("TotalSeats"), Fare = r.Dec("BaseFare"), From = r.Str("FromCode"), To = r.Str("ToCode") },
            ("@f", Id));
        if (f == null || f.St == "cancelled" || f.Dep <= DateTime.Now)
        {
            Err("This flight is not open for booking.");
            return false;
        }
        FlightNumber = f.Num;
        RouteText = f.From + " → " + f.To;
        Dep = f.Dep;
        Model = f.Model;
        var fares = _svc.FareClasses();
        Economy = f.Fare * fares["Economy"].PriceMultiplier;
        Business = f.Fare * fares["Business"].PriceMultiplier;
        Cabins = SeatLayout.Build(f.Seats, out var exits);
        ExitRows = exits;
        Taken = Db.Query("SELECT SeatNumber FROM Seat WHERE FlightID = @f AND IsAvailable = 0", r => r.Str("SeatNumber"), ("@f", Id)).ToHashSet();
        if (PassengerId != null)
        {
            Miles = _svc.MilesBalance(PassengerId.Value);
            MaxUnits = Math.Min(BookingService.MaxDiscountUnits, Miles / BookingService.MilesPerDiscount);
        }
        return true;
    }
}
