namespace Aerion.Pages.Admin;

public class FlightsModel : AppPage
{
    public FlightsModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    [BindProperty] public int? EditId { get; set; }
    [BindProperty] public int RouteId { get; set; }
    [BindProperty] public int AircraftId { get; set; }
    [BindProperty] public string FlightNumber { get; set; } = "";
    [BindProperty] public DateTime Departure { get; set; } = DateTime.Today.AddDays(1).AddHours(9);
    [BindProperty] public DateTime Arrival { get; set; } = DateTime.Today.AddDays(1).AddHours(12);

    public List<(int Id, string Text)> Routes { get; set; } = new();
    public List<(int Id, string Text, int Seats)> Aircraft { get; set; } = new();
    public List<(int Id, string Number, string Route, DateTime Dep, DateTime Arr, string Status, string Plane, int RouteId, int PlaneId)> Flights { get; set; } = new();

    public void OnGet(int? edit)
    {
        Load();
        if (edit != null)
        {
            var f = Flights.FirstOrDefault(x => x.Id == edit);
            if (f.Id != 0)
            {
                EditId = f.Id; RouteId = f.RouteId; AircraftId = f.PlaneId; FlightNumber = f.Number; Departure = f.Dep; Arrival = f.Arr;
            }
        }
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(FlightNumber) || FlightNumber.Length > 10) Err("Enter a flight number (up to 10 characters).");
        else if (Arrival <= Departure) Err("Arrival must be after departure.");
        else if (EditId == null)
        {
            if (Departure <= DateTime.Now) { Err("Departure must be in the future."); return RedirectToPage(); }
            int seats = Db.Int("SELECT TotalSeats FROM Aircraft WHERE AircraftID = @a", ("@a", AircraftId));
            // Seats are created from the aircraft size, so the cabin map always matches the plane.
            Seeder.AddFlight(Db, RouteId, AircraftId, seats, FlightNumber.Trim().ToUpper(), Departure, Arrival);
            Ok("Flight added with its seats.");
        }
        else
        {
            // The aircraft is not changed after seats exist; times, number and route can be.
            Db.Execute("UPDATE Flight SET RouteID=@r, FlightNumber=@n, DepartureTime=@d, ArrivalTime=@a WHERE FlightID=@f",
                ("@r", RouteId), ("@n", FlightNumber.Trim().ToUpper()), ("@d", Departure), ("@a", Arrival), ("@f", EditId));
            Ok("Flight updated.");
        }
        return RedirectToPage();
    }

    private void Load()
    {
        Routes = Db.Query("SELECT r.RouteID, a.City + ' (' + a.Code + ') → ' + b.City + ' (' + b.Code + ')' AS T FROM Route r JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID ORDER BY a.City, b.City", r => (r.Int("RouteID"), r.Str("T")));
        Aircraft = Db.Query("SELECT AircraftID, RegistrationNumber + ' ' + Model AS T, TotalSeats FROM Aircraft", r => (r.Int("AircraftID"), r.Str("T"), r.Int("TotalSeats")));
        Flights = Db.Query(
            "SELECT TOP 80 f.FlightID, f.FlightNumber, a.Code + ' → ' + b.Code AS Route, f.DepartureTime, f.ArrivalTime, f.Status, ac.Model, f.RouteID, f.AircraftID " +
            "FROM Flight f JOIN Route r ON r.RouteID = f.RouteID JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID " +
            "JOIN Aircraft ac ON ac.AircraftID = f.AircraftID ORDER BY f.DepartureTime DESC",
            r => (r.Int("FlightID"), r.Str("FlightNumber"), r.Str("Route"), r.Date("DepartureTime"), r.Date("ArrivalTime"), r.Str("Status"), r.Str("Model"), r.Int("RouteID"), r.Int("AircraftID")));
    }
}
