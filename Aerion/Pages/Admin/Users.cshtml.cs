using System.Text.RegularExpressions;

namespace Aerion.Pages.Admin;

public class UsersModel : AppPage
{
    public UsersModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    public List<(string Username, string Role, string Name)> Accounts { get; set; } = new();

    public void OnGet()
    {
        Accounts = Db.Query("SELECT u.Username, u.Role, ISNULL(e.Name, p.Name) AS Name FROM UserAccount u LEFT JOIN Employee e ON e.UserID = u.UserID LEFT JOIN Passenger p ON p.UserID = u.UserID WHERE u.Role <> 'passenger' ORDER BY u.Role, u.Username", r => (r.Str("Username"), r.Str("Role"), r.Str("Name")));
    }

    public IActionResult OnPost(string username, string name, string role, string password)
    {
        if (role != "staff" && role != "admin") Err("Choose staff or admin.");
        else if (!Regex.IsMatch(username ?? "", "^[A-Za-z0-9_.-]{3,50}$") || string.IsNullOrWhiteSpace(name)) Err("Enter a valid username and name.");
        else if ((password ?? "").Length < 8) Err("Password must be at least 8 characters.");
        else if (Db.Int("SELECT COUNT(*) FROM UserAccount WHERE Username = @u", ("@u", username)) > 0) Err("Username already exists.");
        else { Seeder.AddUser(Db, username, password!, role, name.Trim(), role == "admin" ? "Administrator" : "Airline staff"); Ok("Account created."); }
        return RedirectToPage();
    }
}
