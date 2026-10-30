namespace Aerion.Pages;

public class LoginModel : AppPage
{
    private static readonly Dictionary<string, List<DateTime>> Failures = new();

    public LoginModel(Db db) : base(db) { }

    [BindProperty(SupportsGet = true)] public string? Next { get; set; }
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";

    public void OnGet() { }

    public IActionResult OnPost()
    {
        string key = (Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "") + "|" + Username.ToLower();
        lock (Failures)
        {
            var recent = Failures.GetValueOrDefault(key, new()).Where(t => t > DateTime.Now.AddMinutes(-5)).ToList();
            Failures[key] = recent;
            if (recent.Count >= 5)
            {
                Err("Too many failed attempts. Try again in a few minutes.");
                return Page();
            }
        }

        var user = Db.One("SELECT UserID, PasswordHash, Role FROM UserAccount WHERE Username = @u",
            r => new { Id = r.Int("UserID"), Hash = r.Str("PasswordHash"), Role = r.Str("Role") }, ("@u", Username));
        if (user == null || !Security.Verify(Password, user.Hash))
        {
            lock (Failures) { Failures[key].Add(DateTime.Now); }
            Err("Invalid username or password.");
            return Page();
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32("uid", user.Id);
        HttpContext.Session.SetString("role", user.Role);
        if (user.Role == "passenger")
        {
            var p = Db.One("SELECT PassengerID, Name FROM Passenger WHERE UserID = @u", r => new { Id = r.Int("PassengerID"), Name = r.Str("Name") }, ("@u", user.Id))!;
            HttpContext.Session.SetInt32("pid", p.Id);
            HttpContext.Session.SetString("name", p.Name);
        }
        else
        {
            var e = Db.One("SELECT EmployeeID, Name FROM Employee WHERE UserID = @u", r => new { Id = r.Int("EmployeeID"), Name = r.Str("Name") }, ("@u", user.Id))!;
            HttpContext.Session.SetInt32("eid", e.Id);
            HttpContext.Session.SetString("name", e.Name);
        }

        // Only local paths are allowed, so nobody can bounce a user to another site.
        if (!string.IsNullOrEmpty(Next) && Url.IsLocalUrl(Next) && user.Role != "admin") return LocalRedirect(Next);
        return RedirectToPage(user.Role == "admin" ? "/Admin/Index" : user.Role == "staff" ? "/Staff/Index" : "/MyBookings");
    }
}
