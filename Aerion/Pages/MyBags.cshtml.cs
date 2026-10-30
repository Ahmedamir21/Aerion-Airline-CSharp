namespace Aerion.Pages;

public class BagRow
{
    public int Id { get; set; }
    public string Tag { get; set; } = "";
    public string Flight { get; set; } = "";
    public string Seat { get; set; } = "";
    public string Status { get; set; } = "";
}

public class MyBagsModel : AppPage
{
    public MyBagsModel(Db db) : base(db) { }

    protected override string[] AllowedRoles => new[] { "passenger" };

    public List<BagRow> Bags { get; set; } = new();

    public void OnGet()
    {
        Bags = Db.Query(
            "SELECT g.BaggageID, g.TagNumber, g.Status, f.FlightNumber, ISNULL(t.SeatNumber, '-') AS Seat " +
            "FROM Baggage g JOIN Ticket t ON t.TicketID = g.TicketID JOIN Booking b ON b.BookingID = t.BookingID " +
            "JOIN Flight f ON f.FlightID = t.FlightID WHERE b.PassengerID = @p AND t.Status <> 'cancelled' ORDER BY f.DepartureTime",
            r => new BagRow { Id = r.Int("BaggageID"), Tag = r.Str("TagNumber"), Status = r.Str("Status"), Flight = r.Str("FlightNumber"), Seat = r.Str("Seat") },
            ("@p", PassengerId));
    }

    public IActionResult OnPostLost(int id)
    {
        // The join on the passenger makes sure nobody can report someone else's bag.
        int n = Db.Execute(
            "UPDATE g SET g.Status = 'lost' FROM Baggage g JOIN Ticket t ON t.TicketID = g.TicketID JOIN Booking b ON b.BookingID = t.BookingID " +
            "WHERE g.BaggageID = @g AND b.PassengerID = @p AND g.Status IN ('checked in','loaded','delayed')",
            ("@g", id), ("@p", PassengerId));
        if (n == 1) Ok("Bag reported lost. The airline staff will look for it."); else Err("Only a checked-in bag can be reported lost.");
        return RedirectToPage();
    }
}
