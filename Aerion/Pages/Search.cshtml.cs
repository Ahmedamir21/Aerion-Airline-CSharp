namespace Aerion.Pages;

public class FlightRow
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public DateTime Dep { get; set; }
    public DateTime Arr { get; set; }
    public string Model { get; set; } = "";
    public decimal Economy { get; set; }
    public decimal Business { get; set; }
    public int Free { get; set; }
    public string Status { get; set; } = "";
}

public class SearchModel : AppPage
{
    public SearchModel(Db db) : base(db) { }

    [BindProperty(SupportsGet = true)] public string? Origin { get; set; }
    [BindProperty(SupportsGet = true)] public string? Destination { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }

    public List<FlightRow> Flights { get; set; } = new();
    public List<(string Code, string City)> Airports { get; set; } = new();
    public bool Searched { get; set; }
    public bool Fallback { get; set; }

    public void OnGet()
    {
        Airports = Db.Query("SELECT Code, City FROM Airport ORDER BY City", r => (r.Str("Code"), r.Str("City")));
        string o = (Origin ?? "").Trim().ToUpper();
        string d = (Destination ?? "").Trim().ToUpper();
        if (o == "" && d == "") return;
        if (o != "" && o == d)
        {
            Err("Choose different departure and arrival airports.");
            return;
        }
        Searched = true;
        DateTime start = Date.HasValue && Date.Value > DateTime.Now ? Date.Value.Date : DateTime.Now;
        Flights = Load(o, d, start);
        if (Date.HasValue && Flights.Count == 0)
        {
            Fallback = true;
            Flights = Load(o, d, DateTime.Now);
        }
    }

    private List<FlightRow> Load(string o, string d, DateTime start)
    {
        // Origin and destination are optional filters: an empty value matches everything.
        return Db.Query(
            "SELECT TOP 40 f.FlightID, f.FlightNumber, f.DepartureTime, f.ArrivalTime, f.Status, ac.Model, r.BaseFare, " +
            "a.Code AS FromCode, b.Code AS ToCode, " +
            "(SELECT COUNT(*) FROM Seat s WHERE s.FlightID = f.FlightID AND s.IsAvailable = 1) AS Free, " +
            "(SELECT PriceMultiplier FROM FareClass WHERE Name = 'Business') AS BizMult " +
            "FROM Flight f JOIN Route r ON r.RouteID = f.RouteID " +
            "JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID " +
            "JOIN Aircraft ac ON ac.AircraftID = f.AircraftID " +
            "WHERE f.DepartureTime >= @start AND f.Status <> 'cancelled' " +
            "AND (@o = '' OR a.Code = @o) AND (@d = '' OR b.Code = @d) ORDER BY f.DepartureTime",
            r => new FlightRow
            {
                Id = r.Int("FlightID"), Number = r.Str("FlightNumber"), From = r.Str("FromCode"), To = r.Str("ToCode"),
                Dep = r.Date("DepartureTime"), Arr = r.Date("ArrivalTime"), Model = r.Str("Model"), Status = r.Str("Status"),
                Economy = r.Dec("BaseFare"), Business = Math.Round(r.Dec("BaseFare") * r.Dec("BizMult"), 2), Free = r.Int("Free")
            },
            ("@start", start), ("@o", o), ("@d", d));
    }
}
