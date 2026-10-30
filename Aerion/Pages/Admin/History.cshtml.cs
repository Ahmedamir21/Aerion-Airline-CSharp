namespace Aerion.Pages.Admin;

public class HistoryModel : AppPage
{
    public HistoryModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public List<(int Id, string Passenger, string Flight, DateTime Booked, decimal Amount, string Status, string By)> Rows { get; set; } = new();

    public void OnGet()
    {
        Rows = Db.Query(
            "SELECT TOP 100 b.BookingID, p.Name, f.FlightNumber, b.BookingDate, ISNULL(SUM(t.FarePaid),0) AS Amount, b.Status, ISNULL(e.Name,'online') AS ByName " +
            "FROM Booking b JOIN Passenger p ON p.PassengerID = b.PassengerID JOIN Flight f ON f.FlightID = b.FlightID LEFT JOIN Ticket t ON t.BookingID = b.BookingID " +
            "LEFT JOIN Employee e ON e.EmployeeID = b.BookedByEmployeeID WHERE (@s IS NULL OR b.Status = @s) " +
            "GROUP BY b.BookingID, p.Name, f.FlightNumber, b.BookingDate, b.Status, e.Name ORDER BY b.BookingDate DESC",
            r => (r.Int("BookingID"), r.Str("Name"), r.Str("FlightNumber"), r.Date("BookingDate"), r.Dec("Amount"), r.Str("Status"), r.Str("ByName")),
            ("@s", string.IsNullOrEmpty(Status) ? null : Status));
    }
}
