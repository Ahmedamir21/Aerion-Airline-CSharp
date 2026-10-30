namespace Aerion.Data;

public record FareInfo(string Name, decimal PriceMultiplier, decimal MilesMultiplier);

public record BookResult(bool Ok, string Message, int BookingId = 0);

// Booking rules live here so the passenger page and the staff counter page share them.
public class BookingService
{
    public const int MilesPerDiscount = 40;   // every 40 miles ...
    public const int DiscountPerUnit = 10;    // ... gives 10% off
    public const int MaxDiscountUnits = 3;    // up to 30%

    private readonly Db _db;

    public BookingService(Db db)
    {
        _db = db;
    }

    public Dictionary<string, FareInfo> FareClasses()
    {
        var list = _db.Query(
            "SELECT Name, PriceMultiplier, MilesMultiplier FROM FareClass",
            r => new FareInfo(r.Str("Name"), r.Dec("PriceMultiplier"), r.Dec("MilesMultiplier")));
        return list.ToDictionary(f => f.Name);
    }

    public int MilesBalance(int passengerId)
    {
        return _db.Int("SELECT MilesBalance FROM LoyaltyAccount WHERE PassengerID = @p", ("@p", passengerId));
    }

    public BookResult Book(int flightId, int passengerId, List<string> seats, string method,
        int milesUnits, int? employeeId)
    {
        if (seats.Count == 0) return new BookResult(false, "Select at least one seat.");
        if (seats.Count > 9) return new BookResult(false, "You can book up to 9 seats at once.");
        if (method != "card" && method != "cash" && method != "bank transfer")
            return new BookResult(false, "Choose a valid payment method.");

        try
        {
            return _db.InTransaction(tx =>
            {
                var flight = tx.Query(
                    "SELECT f.Status, f.DepartureTime, r.BaseFare, r.DistanceKm " +
                    "FROM Flight f JOIN Route r ON r.RouteID = f.RouteID WHERE f.FlightID = @f",
                    r => new { Status = r.Str("Status"), Dep = r.Date("DepartureTime"), Fare = r.Dec("BaseFare"), Km = r.Int("DistanceKm") },
                    ("@f", flightId)).FirstOrDefault();
                if (flight == null) return new BookResult(false, "Flight not found.");
                if (flight.Status == "cancelled" || flight.Dep <= DateTime.Now)
                    return new BookResult(false, "This flight is no longer open for booking.");

                // Discount from loyalty miles (only valid for the passenger's own balance).
                int balance = Convert.ToInt32(tx.Scalar("SELECT MilesBalance FROM LoyaltyAccount WITH (UPDLOCK, HOLDLOCK) WHERE PassengerID = @p", ("@p", passengerId)) ?? 0);
                int units = Math.Max(0, Math.Min(milesUnits, Math.Min(MaxDiscountUnits, balance / MilesPerDiscount)));
                decimal discount = units * DiscountPerUnit / 100m;

                int bookingId = Convert.ToInt32(tx.Scalar(
                    "INSERT INTO Booking (PassengerID, FlightID, BookedByEmployeeID, DiscountPercent, MilesRedeemed) " +
                    "OUTPUT INSERTED.BookingID VALUES (@p, @f, @e, @d, @m)",
                    ("@p", passengerId), ("@f", flightId), ("@e", employeeId),
                    ("@d", units * DiscountPerUnit), ("@m", units * MilesPerDiscount)));

                decimal total = 0;
                int earned = 0;
                foreach (var seat in seats)
                {
                    // The conditional UPDATE is what stops two people taking the same seat.
                    int taken = tx.Execute(
                        "UPDATE Seat SET IsAvailable = 0 WHERE FlightID = @f AND SeatNumber = @s AND IsAvailable = 1",
                        ("@f", flightId), ("@s", seat));
                    if (taken != 1) throw new InvalidOperationException("Seat " + seat + " is no longer available.");

                    var fare = tx.Query(
                        "SELECT fc.PriceMultiplier, fc.MilesMultiplier FROM Seat s " +
                        "JOIN FareClass fc ON fc.FareClassID = s.FareClassID WHERE s.FlightID = @f AND s.SeatNumber = @s",
                        r => new { Price = r.Dec("PriceMultiplier"), Miles = r.Dec("MilesMultiplier") },
                        ("@f", flightId), ("@s", seat)).First();

                    decimal paid = Math.Round(flight.Fare * fare.Price * (1 - discount), 2);
                    total += paid;
                    earned += (int)(flight.Km / 100 * fare.Miles);

                    int ticketId = Convert.ToInt32(tx.Scalar(
                        "INSERT INTO Ticket (BookingID, FlightID, SeatNumber, FarePaid) OUTPUT INSERTED.TicketID " +
                        "VALUES (@b, @f, @s, @fare)",
                        ("@b", bookingId), ("@f", flightId), ("@s", seat), ("@fare", paid)));
                    tx.Execute(
                        "INSERT INTO Baggage (TicketID, TagNumber, WeightKg) VALUES (@t, @tag, @w)",
                        ("@t", ticketId), ("@tag", "AR" + ticketId.ToString("D7")), ("@w", 0m));
                }

                tx.Execute("INSERT INTO Payment (BookingID, Amount, Method) VALUES (@b, @a, @m)",
                    ("@b", bookingId), ("@a", total), ("@m", method));

                // Miles: spend first, then earn on this booking.
                int loyaltyId = Convert.ToInt32(tx.Scalar("SELECT LoyaltyID FROM LoyaltyAccount WHERE PassengerID = @p", ("@p", passengerId)));
                if (units > 0)
                {
                    tx.Execute("INSERT INTO LoyaltyTransaction (LoyaltyID, Miles, Reason) VALUES (@l, @m, @r)",
                        ("@l", loyaltyId), ("@m", -units * MilesPerDiscount), ("@r", $"Booking {bookingId}: {units * DiscountPerUnit}% discount"));
                }
                tx.Execute("INSERT INTO LoyaltyTransaction (LoyaltyID, Miles, Reason) VALUES (@l, @m, @r)",
                    ("@l", loyaltyId), ("@m", earned), ("@r", $"Booking {bookingId}: miles earned"));
                tx.Execute("UPDATE LoyaltyAccount SET MilesBalance = MilesBalance + @d WHERE LoyaltyID = @l",
                    ("@d", earned - units * MilesPerDiscount), ("@l", loyaltyId));
                tx.Execute("UPDATE Booking SET MilesEarned = @e WHERE BookingID = @b", ("@e", earned), ("@b", bookingId));

                string note = units > 0 ? $" ({units * DiscountPerUnit}% miles discount applied)" : "";
                return new BookResult(true,
                    $"Booking {bookingId} confirmed: seat{(seats.Count > 1 ? "s" : "")} {string.Join(", ", seats)}. Total ${total:N2}{note}.",
                    bookingId);
            });
        }
        catch (InvalidOperationException ex)
        {
            return new BookResult(false, ex.Message);
        }
        catch (SqlException)
        {
            return new BookResult(false, "Could not save the booking. Please try again.");
        }
    }

    public BookResult Cancel(int bookingId, int? onlyPassengerId)
    {
        try
        {
            return _db.InTransaction(tx =>
            {
                var b = tx.Query(
                    "SELECT b.PassengerID, b.Status, b.MilesEarned, b.MilesRedeemed, f.DepartureTime " +
                    "FROM Booking b JOIN Flight f ON f.FlightID = b.FlightID WHERE b.BookingID = @b",
                    r => new { Pid = r.Int("PassengerID"), Status = r.Str("Status"), Earned = r.Int("MilesEarned"), Redeemed = r.Int("MilesRedeemed"), Dep = r.Date("DepartureTime") },
                    ("@b", bookingId)).FirstOrDefault();
                if (b == null || (onlyPassengerId != null && b.Pid != onlyPassengerId))
                    return new BookResult(false, "Booking not found.");
                if (b.Status == "cancelled") return new BookResult(false, "This booking is already cancelled.");
                if (b.Dep <= DateTime.Now) return new BookResult(false, "A flight that already left cannot be cancelled.");

                // Only one request can flip the status, so miles are never refunded twice.
                int flipped = tx.Execute("UPDATE Booking SET Status = 'cancelled', CancelledAt = SYSDATETIME() WHERE BookingID = @b AND Status = 'confirmed'", ("@b", bookingId));
                if (flipped != 1) return new BookResult(false, "This booking is already cancelled.");

                // Free the seats first (needs the ticket rows), then clear them from the tickets.
                tx.Execute(
                    "UPDATE s SET s.IsAvailable = 1 FROM Seat s JOIN Ticket t ON t.FlightID = s.FlightID AND t.SeatNumber = s.SeatNumber " +
                    "WHERE t.BookingID = @b", ("@b", bookingId));
                tx.Execute("UPDATE Ticket SET Status = 'cancelled', SeatNumber = NULL WHERE BookingID = @b", ("@b", bookingId));
                tx.Execute("UPDATE Payment SET Status = 'refunded' WHERE BookingID = @b", ("@b", bookingId));

                // Give back spent miles and take back the earned ones.
                int loyaltyId = Convert.ToInt32(tx.Scalar("SELECT LoyaltyID FROM LoyaltyAccount WHERE PassengerID = @p", ("@p", b.Pid)));
                int change = b.Redeemed - b.Earned;
                tx.Execute("INSERT INTO LoyaltyTransaction (LoyaltyID, Miles, Reason) VALUES (@l, @m, @r)",
                    ("@l", loyaltyId), ("@m", change), ("@r", $"Booking {bookingId} cancelled"));
                tx.Execute("UPDATE LoyaltyAccount SET MilesBalance = CASE WHEN MilesBalance + @c < 0 THEN 0 ELSE MilesBalance + @c END WHERE LoyaltyID = @l",
                    ("@c", change), ("@l", loyaltyId));
                return new BookResult(true, $"Booking {bookingId} cancelled and the payment was refunded.", bookingId);
            });
        }
        catch (SqlException)
        {
            return new BookResult(false, "Could not cancel the booking. Please try again.");
        }
    }

    // Move a ticket to another free seat of the same class (passenger "change" and staff move).
    public BookResult ChangeSeat(int ticketId, string newSeat, int? onlyPassengerId)
    {
        try
        {
            return _db.InTransaction(tx =>
            {
                var t = tx.Query(
                    "SELECT t.FlightID, t.SeatNumber, t.Status, b.PassengerID, s.FareClassID, f.DepartureTime " +
                    "FROM Ticket t JOIN Booking b ON b.BookingID = t.BookingID " +
                    "JOIN Flight f ON f.FlightID = t.FlightID " +
                    "JOIN Seat s ON s.FlightID = t.FlightID AND s.SeatNumber = t.SeatNumber " +
                    "WHERE t.TicketID = @t",
                    r => new { Flight = r.Int("FlightID"), Seat = r.Str("SeatNumber"), Status = r.Str("Status"), Pid = r.Int("PassengerID"), Fc = r.Int("FareClassID"), Dep = r.Date("DepartureTime") },
                    ("@t", ticketId)).FirstOrDefault();
                if (t == null || (onlyPassengerId != null && t.Pid != onlyPassengerId))
                    return new BookResult(false, "Ticket not found.");
                if (t.Status == "cancelled") return new BookResult(false, "This ticket is cancelled.");
                if (t.Dep <= DateTime.Now) return new BookResult(false, "The flight already left.");
                if (t.Seat == newSeat) return new BookResult(false, "That is already your seat.");

                int sameClass = Convert.ToInt32(tx.Scalar(
                    "SELECT COUNT(*) FROM Seat WHERE FlightID = @f AND SeatNumber = @s AND FareClassID = @c AND IsAvailable = 1",
                    ("@f", t.Flight), ("@s", newSeat), ("@c", t.Fc)));
                if (sameClass != 1) return new BookResult(false, "Pick a free seat in the same class.");

                tx.Execute("UPDATE Seat SET IsAvailable = 0 WHERE FlightID = @f AND SeatNumber = @s AND IsAvailable = 1", ("@f", t.Flight), ("@s", newSeat));
                tx.Execute("UPDATE Seat SET IsAvailable = 1 WHERE FlightID = @f AND SeatNumber = @s", ("@f", t.Flight), ("@s", t.Seat));
                tx.Execute("UPDATE Ticket SET SeatNumber = @s WHERE TicketID = @t", ("@s", newSeat), ("@t", ticketId));
                return new BookResult(true, $"Seat changed from {t.Seat} to {newSeat}.");
            });
        }
        catch (SqlException)
        {
            return new BookResult(false, "Could not change the seat. Please try again.");
        }
    }
}
