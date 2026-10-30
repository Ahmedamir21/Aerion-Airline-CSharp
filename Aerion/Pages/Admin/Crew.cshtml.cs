namespace Aerion.Pages.Admin;

public class CrewModel : AppPage
{
    public CrewModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    [BindProperty(SupportsGet = true)] public int? FlightId { get; set; }
    public List<(int Id, string Text)> Flights { get; set; } = new();
    public List<(int Id, string Name, string Role)> Assigned { get; set; } = new();
    public List<(int Id, string Name, string Role)> Available { get; set; } = new();

    public void OnGet()
    {
        Flights = Db.Query("SELECT TOP 80 f.FlightID, f.FlightNumber + ' ' + FORMAT(f.DepartureTime, 'dd MMM HH:mm') AS T FROM Flight f WHERE f.DepartureTime > SYSDATETIME() ORDER BY f.DepartureTime", r => (r.Int("FlightID"), r.Str("T")));
        if (FlightId == null) return;
        Assigned = Db.Query("SELECT c.CrewID, c.Name, c.CrewRole FROM FlightCrew fc JOIN CrewMember c ON c.CrewID = fc.CrewID WHERE fc.FlightID = @f", r => (r.Int("CrewID"), r.Str("Name"), r.Str("CrewRole")), ("@f", FlightId));
        // A crew member already on another flight at the same time is not offered.
        Available = Db.Query(
            "SELECT c.CrewID, c.Name, c.CrewRole FROM CrewMember c WHERE c.CrewID NOT IN (SELECT CrewID FROM FlightCrew WHERE FlightID = @f) " +
            "AND NOT EXISTS (SELECT 1 FROM FlightCrew x JOIN Flight f2 ON f2.FlightID = x.FlightID, Flight f1 " +
            "WHERE x.CrewID = c.CrewID AND f1.FlightID = @f AND f2.DepartureTime < f1.ArrivalTime AND f2.ArrivalTime > f1.DepartureTime) ORDER BY c.CrewRole, c.Name",
            r => (r.Int("CrewID"), r.Str("Name"), r.Str("CrewRole")), ("@f", FlightId));
    }

    public IActionResult OnPostAdd(int flightId, int crewId)
    {
        Db.Execute("INSERT INTO FlightCrew (FlightID, CrewID) VALUES (@f,@c)", ("@f", flightId), ("@c", crewId));
        Ok("Crew member assigned.");
        return RedirectToPage(new { flightId });
    }

    public IActionResult OnPostRemove(int flightId, int crewId)
    {
        Db.Execute("DELETE FROM FlightCrew WHERE FlightID=@f AND CrewID=@c", ("@f", flightId), ("@c", crewId));
        Ok("Crew member removed.");
        return RedirectToPage(new { flightId });
    }
}
