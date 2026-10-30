using System.Text.RegularExpressions;

namespace Aerion.Pages;

public class RegisterModel : AppPage
{
    public RegisterModel(Db db) : base(db) { }

    [BindProperty(SupportsGet = true)] public string? Next { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Passport { get; set; } = "";
    [BindProperty] public string? Contact { get; set; }
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Confirm { get; set; } = "";

    public void OnGet() { }

    public IActionResult OnPost()
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100) error = "Enter your full name.";
        else if (!Regex.IsMatch(Username ?? "", "^[A-Za-z0-9_.-]{3,50}$")) error = "Username: 3-50 letters, digits, dot, dash or underscore.";
        else if (Password.Length < 8) error = "Password must be at least 8 characters.";
        else if (Password != Confirm) error = "Passwords do not match.";
        else if (!Regex.IsMatch((Passport ?? "").ToUpper(), "^[A-Z0-9]{5,20}$")) error = "Enter a valid passport number.";
        else if (Db.Int("SELECT COUNT(*) FROM UserAccount WHERE Username = @u", ("@u", Username)) > 0) error = "Username already exists.";
        else if (Db.Int("SELECT COUNT(*) FROM Passenger WHERE PassportNumber = @p", ("@p", Passport.ToUpper())) > 0) error = "That passport number is already registered.";

        if (error != null)
        {
            Err(error);
            return Page();
        }

        int pid = Seeder.AddPassenger(Db, Username, Password, Name.Trim(), Passport.ToUpper(), 0);
        if (!string.IsNullOrWhiteSpace(Contact))
            Db.Execute("UPDATE Passenger SET Contact = @c WHERE PassengerID = @p", ("@c", Contact), ("@p", pid));

        int uid = Db.Int("SELECT UserID FROM Passenger WHERE PassengerID = @p", ("@p", pid));
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32("uid", uid);
        HttpContext.Session.SetString("role", "passenger");
        HttpContext.Session.SetInt32("pid", pid);
        HttpContext.Session.SetString("name", Name.Trim());
        Ok("Account created. Welcome aboard.");
        if (!string.IsNullOrEmpty(Next) && Url.IsLocalUrl(Next)) return LocalRedirect(Next);
        return RedirectToPage("/MyBookings");
    }
}
