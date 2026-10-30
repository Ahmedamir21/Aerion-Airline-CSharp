namespace Aerion.Pages.Staff;

public class FlightStatusModel : AppPage
{
    public FlightStatusModel(Db db) : base(db) { }

    protected override string[] AllowedRoles => new[] { "staff" };

    public List<(int Id, string Number, string Route, DateTime Dep, string Status)> Flights { get; set; } = new();
    public List<(string Name, string Passport, string Seat, string Status)> Passengers { get; set; } = new();
    [BindProperty(SupportsGet = true)] public int? List { get; set; }

    public void OnGet()
    {
        Flights = Db.Query(
            "SELECT TOP 60 f.FlightID, f.FlightNumber, a.Code + ' → ' + b.Code AS Route, f.DepartureTime, f.Status FROM Flight f " +
            "JOIN Route r ON r.RouteID = f.RouteID JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID " +
            "WHERE f.DepartureTime > DATEADD(day, -1, SYSDATETIME()) ORDER BY f.DepartureTime",
            r => (r.Int("FlightID"), r.Str("FlightNumber"), r.Str("Route"), r.Date("DepartureTime"), r.Str("Status")));
        if (List != null)
        {
            Passengers = Db.Query(
                "SELECT p.Name, p.PassportNumber, ISNULL(t.SeatNumber,'-') AS Seat, t.Status FROM Ticket t JOIN Booking b ON b.BookingID = t.BookingID " +
                "JOIN Passenger p ON p.PassengerID = b.PassengerID WHERE t.FlightID = @f AND t.Status <> 'cancelled' ORDER BY t.SeatNumber",
                r => (r.Str("Name"), r.Str("PassportNumber"), r.Str("Seat"), r.Str("Status")), ("@f", List));
        }
    }

    public IActionResult OnPostStatus(int id, string status)
    {
        if (status != "on time" && status != "delayed" && status != "cancelled") { Err("Invalid status."); return RedirectToPage(); }
        Db.Execute("UPDATE Flight SET Status = @s WHERE FlightID = @f", ("@s", status), ("@f", id));
        Ok("Flight status updated.");
        return RedirectToPage();
    }
}
