using System.Text.RegularExpressions;

namespace Aerion.Pages;

// Staff and admins add their passport once, then fly like any passenger with the same login.
public class PassengerProfileModel : AppPage
{
    public PassengerProfileModel(Db db) : base(db) { }

    protected override string[] AllowedRoles => new[] { "staff", "admin" };

    [BindProperty] public string Passport { get; set; } = "";
    [BindProperty] public string? Contact { get; set; }

    public int Discount => BookingService.StaffDiscountPercent;
    public string DisplayName => HttpContext.Session.GetString("name") ?? "";

    public IActionResult OnGet()
    {
        return PassengerId != null ? RedirectToPage("/MyBookings") : Page();
    }

    public IActionResult OnPost()
    {
        if (PassengerId != null) return RedirectToPage("/MyBookings");

        string passport = (Passport ?? "").Trim().ToUpper();
        if (!Regex.IsMatch(passport, "^[A-Z0-9]{5,20}$"))
        {
            Err("Enter a valid passport number.");
            return Page();
        }
        if (Db.Int("SELECT COUNT(*) FROM Passenger WHERE PassportNumber = @p", ("@p", passport)) > 0)
        {
            Err("That passport number is already registered.");
            return Page();
        }

        int pid = Seeder.AddPassengerProfile(Db, UserId!.Value, DisplayName, passport);
        if (!string.IsNullOrWhiteSpace(Contact))
            Db.Execute("UPDATE Passenger SET Contact = @c WHERE PassengerID = @p", ("@c", Contact.Trim()), ("@p", pid));

        HttpContext.Session.SetInt32("pid", pid);
        Ok($"Passenger profile created. You get {Discount}% off every booking.");
        return RedirectToPage("/Search");
    }
}
