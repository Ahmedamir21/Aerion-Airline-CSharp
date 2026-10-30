namespace Aerion.Pages.Admin;

public class ReportsModel : AppPage
{
    public ReportsModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    public List<(string Route, int Sold, decimal Revenue)> ByRoute { get; set; } = new();
    public List<(string Flight, string Route, DateTime Dep, int Total, int Sold, double Percent)> Fill { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public int Bookings { get; set; }
    public int Cancelled { get; set; }

    public void OnGet()
    {
        // Revenue per route (only tickets that were not cancelled).
        ByRoute = Db.Query(
            "SELECT a.Code + ' → ' + b.Code AS Route, COUNT(t.TicketID) AS Sold, ISNULL(SUM(t.FarePaid),0) AS Revenue " +
            "FROM Route r JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID " +
            "JOIN Flight f ON f.RouteID = r.RouteID LEFT JOIN Ticket t ON t.FlightID = f.FlightID AND t.Status <> 'cancelled' " +
            "GROUP BY a.Code, b.Code ORDER BY Revenue DESC",
            r => (r.Str("Route"), r.Int("Sold"), r.Dec("Revenue")));
        // How full each flight is.
        Fill = Db.Query(
            "SELECT TOP 40 f.FlightNumber, a.Code + ' → ' + b.Code AS Route, f.DepartureTime, COUNT(s.SeatID) AS Total, SUM(CASE WHEN s.IsAvailable = 0 THEN 1 ELSE 0 END) AS Sold " +
            "FROM Flight f JOIN Route r ON r.RouteID = f.RouteID JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID " +
            "JOIN Seat s ON s.FlightID = f.FlightID GROUP BY f.FlightNumber, a.Code, b.Code, f.DepartureTime ORDER BY 100.0 * SUM(CASE WHEN s.IsAvailable = 0 THEN 1 ELSE 0 END) / COUNT(s.SeatID) DESC",
            r => (r.Str("FlightNumber"), r.Str("Route"), r.Date("DepartureTime"), r.Int("Total"), r.Int("Sold"), r.Int("Total") == 0 ? 0 : 100.0 * r.Int("Sold") / r.Int("Total")));
        TotalRevenue = ByRoute.Sum(x => x.Revenue);
        Bookings = Db.Int("SELECT COUNT(*) FROM Booking WHERE Status = 'confirmed'");
        Cancelled = Db.Int("SELECT COUNT(*) FROM Booking WHERE Status = 'cancelled'");
    }
}
