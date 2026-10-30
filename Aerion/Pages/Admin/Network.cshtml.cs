namespace Aerion.Pages.Admin;

public class NetworkModel : AppPage
{
    public NetworkModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    public List<(int Id, string Text)> Airports { get; set; } = new();
    public List<(string Route, int Km, decimal Fare)> Routes { get; set; } = new();
    public List<(string Reg, string Model, int Seats)> Aircraft { get; set; } = new();

    public void OnGet()
    {
        Airports = Db.Query("SELECT AirportID, City + ' (' + Code + ')' AS T FROM Airport ORDER BY City", r => (r.Int("AirportID"), r.Str("T")));
        Routes = Db.Query("SELECT a.Code + ' → ' + b.Code AS R, r.DistanceKm, r.BaseFare FROM Route r JOIN Airport a ON a.AirportID = r.DepartureAirportID JOIN Airport b ON b.AirportID = r.ArrivalAirportID ORDER BY a.Code, b.Code", r => (r.Str("R"), r.Int("DistanceKm"), r.Dec("BaseFare")));
        Aircraft = Db.Query("SELECT RegistrationNumber, Model, TotalSeats FROM Aircraft", r => (r.Str("RegistrationNumber"), r.Str("Model"), r.Int("TotalSeats")));
    }

    public IActionResult OnPostAirport(string code, string name, string city, string country)
    {
        code = (code ?? "").Trim().ToUpper();
        if (code.Length < 3 || code.Length > 5 || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(city)) Err("Fill code, name and city.");
        else if (Db.Int("SELECT COUNT(*) FROM Airport WHERE Code = @c", ("@c", code)) > 0) Err("That airport code exists.");
        else { Db.Execute("INSERT INTO Airport (Code, Name, City, Country) VALUES (@c,@n,@t,@y)", ("@c", code), ("@n", name), ("@t", city), ("@y", country ?? "")); Ok("Airport added."); }
        return RedirectToPage();
    }

    public IActionResult OnPostRoute(int from, int to, int km, decimal fare)
    {
        if (from == to || km <= 0 || fare <= 0) Err("Choose two different airports, a distance and a fare.");
        else if (Db.Int("SELECT COUNT(*) FROM Route WHERE DepartureAirportID=@a AND ArrivalAirportID=@b", ("@a", from), ("@b", to)) > 0) Err("That route exists.");
        else { Db.Execute("INSERT INTO Route (DepartureAirportID, ArrivalAirportID, DistanceKm, BaseFare) VALUES (@a,@b,@k,@f)", ("@a", from), ("@b", to), ("@k", km), ("@f", fare)); Ok("Route added."); }
        return RedirectToPage();
    }

    public IActionResult OnPostAircraft(string reg, string model, int seats)
    {
        if (string.IsNullOrWhiteSpace(reg) || string.IsNullOrWhiteSpace(model) || seats < 20 || seats > 500) Err("Fill registration, model and seats (20-500).");
        else if (Db.Int("SELECT COUNT(*) FROM Aircraft WHERE RegistrationNumber = @r", ("@r", reg)) > 0) Err("That registration exists.");
        else { Db.Execute("INSERT INTO Aircraft (RegistrationNumber, Model, TotalSeats) VALUES (@r,@m,@s)", ("@r", reg.Trim().ToUpper()), ("@m", model), ("@s", seats)); Ok("Aircraft added."); }
        return RedirectToPage();
    }
}
