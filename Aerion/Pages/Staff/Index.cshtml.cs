namespace Aerion.Pages.Staff;

public class TicketRow
{
    public int BookingId { get; set; }
    public int TicketId { get; set; }
    public string Passenger { get; set; } = "";
    public string Flight { get; set; } = "";
    public DateTime Dep { get; set; }
    public string Seat { get; set; } = "";
    public string Status { get; set; } = "";
    public string BookingStatus { get; set; } = "";
    public int? BagId { get; set; }
    public string? BagTag { get; set; }
    public string? BagStatus { get; set; }
}

public class IndexModel : AppPage
{
    private readonly BookingService _svc;

    public IndexModel(Db db, BookingService svc) : base(db)
    {
        _svc = svc;
    }

    protected override string[] AllowedRoles => new[] { "staff" };

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public List<TicketRow> Rows { get; set; } = new();

    public void OnGet()
    {
        if (string.IsNullOrWhiteSpace(Q)) return;
        // Search by booking number or by part of the passenger's name.
        int.TryParse(Q.Trim().TrimStart('#'), out int number);
        Rows = Db.Query(
            "SELECT TOP 50 b.BookingID, t.TicketID, p.Name, f.FlightNumber, f.DepartureTime, ISNULL(t.SeatNumber,'-') AS Seat, t.Status, b.Status AS BStatus, " +
            "g.BaggageID, g.TagNumber, g.Status AS BagStatus " +
            "FROM Booking b JOIN Passenger p ON p.PassengerID = b.PassengerID JOIN Ticket t ON t.BookingID = b.BookingID " +
            "JOIN Flight f ON f.FlightID = b.FlightID LEFT JOIN Baggage g ON g.TicketID = t.TicketID " +
            "WHERE b.BookingID = @n OR p.Name LIKE @like ORDER BY f.DepartureTime DESC",
            r => new TicketRow { BookingId = r.Int("BookingID"), TicketId = r.Int("TicketID"), Passenger = r.Str("Name"), Flight = r.Str("FlightNumber"), Dep = r.Date("DepartureTime"), Seat = r.Str("Seat"), Status = r.Str("Status"), BookingStatus = r.Str("BStatus"), BagId = r.NInt("BaggageID"), BagTag = r.NStr("TagNumber"), BagStatus = r.NStr("BagStatus") },
            ("@n", number), ("@like", "%" + Q.Trim() + "%"));
    }

    public IActionResult OnPostCheckIn(int ticketId, string q)
    {
        int n = Db.Execute(
            "UPDATE t SET t.Status = 'checked in' FROM Ticket t JOIN Booking b ON b.BookingID = t.BookingID " +
            "WHERE t.TicketID = @t AND t.Status = 'issued' AND b.Status = 'confirmed'", ("@t", ticketId));
        if (n == 1)
        {
            Db.Execute("UPDATE Baggage SET Status = 'checked in' WHERE TicketID = @t AND Status = 'not checked in'", ("@t", ticketId));
            Ok("Passenger checked in.");
        }
        else Err("This ticket cannot be checked in.");
        return RedirectToPage(new { q });
    }

    public IActionResult OnPostMoveSeat(int ticketId, string seat, string q)
    {
        var res = _svc.ChangeSeat(ticketId, (seat ?? "").Trim().ToUpper(), null);
        if (res.Ok) Ok(res.Message); else Err(res.Message);
        return RedirectToPage(new { q });
    }

    public IActionResult OnPostBag(int bagId, string status, decimal? weight, string q)
    {
        string[] allowed = { "checked in", "loaded", "delayed", "lost", "delivered" };
        if (!allowed.Contains(status)) { Err("Choose a valid bag status."); return RedirectToPage(new { q }); }
        // Registering the weight happens at check-in; later this form marks delayed or lost.
        Db.Execute("UPDATE Baggage SET Status = @s, WeightKg = CASE WHEN @w IS NULL THEN WeightKg ELSE @w END WHERE BaggageID = @b",
            ("@s", status), ("@w", weight), ("@b", bagId));
        Ok("Bag updated.");
        return RedirectToPage(new { q });
    }
}
