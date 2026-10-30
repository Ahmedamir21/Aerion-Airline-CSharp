namespace Aerion.Pages;

public class BookingRow
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Flight { get; set; } = "";
    public string Route { get; set; } = "";
    public DateTime Dep { get; set; }
    public string Seat { get; set; } = "";
    public decimal Paid { get; set; }
    public string Status { get; set; } = "";
    public int FlightId { get; set; }
}

public class MyBookingsModel : AppPage
{
    private readonly BookingService _svc;

    public MyBookingsModel(Db db, BookingService svc) : base(db)
    {
        _svc = svc;
    }

    protected override string[] AllowedRoles => new[] { "passenger" };

    public List<BookingRow> Upcoming { get; set; } = new();
    public List<BookingRow> Past { get; set; } = new();
    [BindProperty(SupportsGet = true)] public int? ChangeTicket { get; set; }
    public List<string> FreeSeats { get; set; } = new();

    public void OnGet()
    {
        var all = Db.Query(
            "SELECT b.BookingID, t.TicketID, t.FlightID, f.FlightNumber, a.Code AS FromCode, c.Code AS ToCode, f.DepartureTime, " +
            "ISNULL(t.SeatNumber, '-') AS Seat, t.FarePaid, b.Status " +
            "FROM Booking b JOIN Ticket t ON t.BookingID = b.BookingID JOIN Flight f ON f.FlightID = b.FlightID " +
            "JOIN Route r ON r.RouteID = f.RouteID JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport c ON c.AirportID = r.ArrivalAirportID " +
            "WHERE b.PassengerID = @p ORDER BY f.DepartureTime DESC",
            r => new BookingRow { Id = r.Int("BookingID"), TicketId = r.Int("TicketID"), FlightId = r.Int("FlightID"), Flight = r.Str("FlightNumber"), Route = r.Str("FromCode") + " → " + r.Str("ToCode"), Dep = r.Date("DepartureTime"), Seat = r.Str("Seat"), Paid = r.Dec("FarePaid"), Status = r.Str("Status") },
            ("@p", PassengerId));
        Upcoming = all.Where(x => x.Dep > DateTime.Now && x.Status == "confirmed").OrderBy(x => x.Dep).ToList();
        Past = all.Except(Upcoming).ToList();
        if (ChangeTicket != null)
        {
            // Only seats of the same class as the current seat can be chosen.
            FreeSeats = Db.Query(
                "SELECT s2.SeatNumber FROM Ticket t JOIN Booking b ON b.BookingID = t.BookingID " +
                "JOIN Seat s1 ON s1.FlightID = t.FlightID AND s1.SeatNumber = t.SeatNumber " +
                "JOIN Seat s2 ON s2.FlightID = t.FlightID AND s2.FareClassID = s1.FareClassID AND s2.IsAvailable = 1 " +
                "WHERE t.TicketID = @t AND b.PassengerID = @p ORDER BY s2.SeatID",
                r => r.Str("SeatNumber"), ("@t", ChangeTicket), ("@p", PassengerId));
        }
    }

    public IActionResult OnPostCancel(int id)
    {
        var res = _svc.Cancel(id, PassengerId);
        if (res.Ok) Ok(res.Message); else Err(res.Message);
        return RedirectToPage();
    }

    public IActionResult OnPostChange(int ticketId, string seat)
    {
        var res = _svc.ChangeSeat(ticketId, seat, PassengerId);
        if (res.Ok) Ok(res.Message); else Err(res.Message);
        return RedirectToPage();
    }
}
