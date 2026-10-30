namespace Aerion.Pages;

public class LogoutModel : AppPage
{
    public LogoutModel(Db db) : base(db) { }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Index");
    }
}
