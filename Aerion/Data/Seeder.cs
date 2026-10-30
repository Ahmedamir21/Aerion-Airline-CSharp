namespace Aerion.Data;

// Fills an empty database with demo data the first time the app runs.
public static class Seeder
{
    public static void Run(Db db)
    {
        if (db.Int("SELECT COUNT(*) FROM Airport") > 0) return;

        db.Execute("INSERT INTO FareClass (Name, PriceMultiplier, MilesMultiplier) VALUES ('Economy',1,1),('Business',2.8,2)");

        var airports = new[]
        {
            ("CAI","Cairo International","Cairo","Egypt"), ("HRG","Hurghada International","Hurghada","Egypt"),
            ("SSH","Sharm El Sheikh International","Sharm El Sheikh","Egypt"), ("LXR","Luxor International","Luxor","Egypt"),
            ("DXB","Dubai International","Dubai","UAE"), ("JED","King Abdulaziz International","Jeddah","Saudi Arabia"),
            ("RUH","King Khalid International","Riyadh","Saudi Arabia"), ("DOH","Hamad International","Doha","Qatar"),
            ("IST","Istanbul Airport","Istanbul","Turkey"), ("LHR","Heathrow","London","UK"),
            ("CDG","Charles de Gaulle","Paris","France"), ("FRA","Frankfurt Airport","Frankfurt","Germany"),
            ("FCO","Fiumicino","Rome","Italy"), ("JFK","John F. Kennedy","New York","USA"), ("NRT","Narita International","Tokyo","Japan")
        };
        foreach (var a in airports)
            db.Execute("INSERT INTO Airport (Code, Name, City, Country) VALUES (@c,@n,@t,@y)",
                ("@c", a.Item1), ("@n", a.Item2), ("@t", a.Item3), ("@y", a.Item4));

        // (destination, km, economy fare in USD) - all routes start from Cairo and come back.
        var routes = new[]
        {
            ("HRG",400,70),("SSH",360,75),("LXR",510,65),("JED",1210,180),("IST",1230,190),("RUH",1630,210),
            ("FCO",2130,280),("DOH",2360,240),("DXB",2420,260),("FRA",2900,310),("CDG",3210,320),("LHR",3510,340),("JFK",9030,690),("NRT",9550,780)
        };
        foreach (var r in routes)
        {
            db.Execute("INSERT INTO Route (DepartureAirportID, ArrivalAirportID, DistanceKm, BaseFare) " +
                       "SELECT a.AirportID, b.AirportID, @k, @f FROM Airport a, Airport b WHERE a.Code='CAI' AND b.Code=@d",
                ("@k", r.Item2), ("@f", r.Item3), ("@d", r.Item1));
            db.Execute("INSERT INTO Route (DepartureAirportID, ArrivalAirportID, DistanceKm, BaseFare) " +
                       "SELECT b.AirportID, a.AirportID, @k, @f FROM Airport a, Airport b WHERE a.Code='CAI' AND b.Code=@d",
                ("@k", r.Item2), ("@f", r.Item3), ("@d", r.Item1));
        }

        var aircraft = new[] { ("SU-AE1","Airbus A320-200",180), ("SU-BE2","Boeing 737-800",189), ("SU-CE3","Airbus A321-200",220), ("SU-DE4","Airbus A330-300",300), ("SU-EE5","Boeing 777-300ER",380) };
        foreach (var a in aircraft)
            db.Execute("INSERT INTO Aircraft (RegistrationNumber, Model, TotalSeats) VALUES (@r,@m,@s)", ("@r", a.Item1), ("@m", a.Item2), ("@s", a.Item3));

        var crew = new[] { ("Capt. Hossam Raafat","pilot"), ("Capt. Mona Fawzy","pilot"), ("F/O Tarek Anwar","co-pilot"), ("F/O Sara Lotfy","co-pilot"), ("Rania Gaber","cabin crew"), ("Karim Saad","cabin crew"), ("Dalia Younis","cabin crew"), ("Amr Zaki","cabin crew") };
        foreach (var c in crew)
            db.Execute("INSERT INTO CrewMember (Name, CrewRole, LicenseNo) VALUES (@n,@r,@l)", ("@n", c.Item1), ("@r", c.Item2), ("@l", "EG-" + Math.Abs(c.Item1.GetHashCode() % 90000 + 10000)));

        AddUser(db, "admin1", "Admin@123", "admin", "Aerion Admin", "Operations");
        AddUser(db, "staff1", "Staff@123", "staff", "Mariam Salah", "Check-in agent");
        AddUser(db, "staff2", "Staff@123", "staff", "Omar Khaled", "Check-in agent");
        var names = new[] { "Omar Hassan", "Nour El-Din", "Mariam Adel", "Youssef Khaled", "Salma Mostafa", "Karim Fathy", "Hana Samir", "Mahmoud Ali", "Laila Ibrahim", "Tamer Nabil" };
        for (int i = 0; i < names.Length; i++)
            AddPassenger(db, "passenger" + (i + 1), "Pass@123", names[i], "A" + (2000100 + i * 37), i == 0 ? 120 : 0);

        // Flights: every route, 4 departures spread over the next weeks.
        var routeRows = db.Query("SELECT RouteID, DistanceKm FROM Route ORDER BY RouteID", r => (Id: r.Int("RouteID"), Km: r.Int("DistanceKm")));
        var planes = db.Query("SELECT AircraftID, TotalSeats FROM Aircraft ORDER BY AircraftID", r => (Id: r.Int("AircraftID"), Seats: r.Int("TotalSeats")));
        int number = 100;
        foreach (var route in routeRows)
        {
            for (int k = 0; k < 4; k++)
            {
                var plane = route.Km > 5000 ? planes[3 + (route.Id + k) % 2] : route.Km < 1000 ? planes[(route.Id + k) % 2] : planes[(route.Id + k) % 4];
                var dep = DateTime.Today.AddDays(1 + k * 4 + route.Id % 3).AddHours(6 + (route.Id * 7 + k * 5) % 16);
                var arr = dep.AddMinutes(route.Km / 800.0 * 60 + 40);
                AddFlight(db, route.Id, plane.Id, plane.Seats, "AR" + (number++), dep, arr);
            }
        }
    }

    public static void AddFlight(Db db, int routeId, int aircraftId, int totalSeats, string flightNumber, DateTime dep, DateTime arr)
    {
        int flightId = db.InTransaction(tx =>
        {
            int id = Convert.ToInt32(tx.Scalar(
                "INSERT INTO Flight (RouteID, AircraftID, FlightNumber, DepartureTime, ArrivalTime) OUTPUT INSERTED.FlightID VALUES (@r,@a,@n,@d,@t)",
                ("@r", routeId), ("@a", aircraftId), ("@n", flightNumber), ("@d", dep), ("@t", arr)));
            foreach (var (seat, cls) in SeatLayout.SeatList(totalSeats))
                tx.Execute("INSERT INTO Seat (FlightID, SeatNumber, FareClassID) SELECT @f, @s, FareClassID FROM FareClass WHERE Name=@c",
                    ("@f", id), ("@s", seat), ("@c", cls));
            return id;
        });
    }

    public static int AddUser(Db db, string username, string password, string role, string name, string position)
    {
        int uid = Convert.ToInt32(db.Scalar("INSERT INTO UserAccount (Username, PasswordHash, Role) OUTPUT INSERTED.UserID VALUES (@u,@p,@r)",
            ("@u", username), ("@p", Security.Hash(password)), ("@r", role)));
        db.Execute("INSERT INTO Employee (UserID, Name, Position) VALUES (@u,@n,@p)", ("@u", uid), ("@n", name), ("@p", position));
        return uid;
    }

    public static int AddPassenger(Db db, string username, string password, string name, string passport, int miles)
    {
        int uid = Convert.ToInt32(db.Scalar("INSERT INTO UserAccount (Username, PasswordHash, Role) OUTPUT INSERTED.UserID VALUES (@u,@p,'passenger')",
            ("@u", username), ("@p", Security.Hash(password))));
        int pid = Convert.ToInt32(db.Scalar("INSERT INTO Passenger (UserID, Name, PassportNumber) OUTPUT INSERTED.PassengerID VALUES (@u,@n,@p)",
            ("@u", uid), ("@n", name), ("@p", passport)));
        db.Execute("INSERT INTO LoyaltyAccount (PassengerID, MilesBalance) VALUES (@p,@m)", ("@p", pid), ("@m", miles));
        return pid;
    }
}
