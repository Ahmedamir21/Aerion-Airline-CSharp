namespace Aerion.Pages;

public class ChangePasswordModel : AppPage
{
    public ChangePasswordModel(Db db) : base(db) { }

    protected override string[] AllowedRoles => new[] { "passenger", "staff", "admin" };

    [BindProperty] public string Current { get; set; } = "";
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string Confirm { get; set; } = "";

    public void OnGet() { }

    public IActionResult OnPost()
    {
        string? hash = Db.Scalar("SELECT PasswordHash FROM UserAccount WHERE UserID = @u", ("@u", UserId)) as string;
        if (hash == null || !Security.Verify(Current, hash)) Err("Current password is wrong.");
        else if (NewPassword.Length < 8) Err("New password must be at least 8 characters.");
        else if (NewPassword != Confirm) Err("New passwords do not match.");
        else
        {
            Db.Execute("UPDATE UserAccount SET PasswordHash = @h WHERE UserID = @u", ("@h", Security.Hash(NewPassword)), ("@u", UserId));
            Ok("Password changed.");
            return RedirectToPage();
        }
        return Page();
    }
}
