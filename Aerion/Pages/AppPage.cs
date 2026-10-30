namespace Aerion.Pages;

// Base class for every page: session helpers, role check and one-line messages.
public abstract class AppPage : PageModel
{
    protected readonly Db Db;

    protected AppPage(Db db)
    {
        Db = db;
    }

    public int? PassengerId => HttpContext.Session.GetInt32("pid");
    public int? EmployeeId => HttpContext.Session.GetInt32("eid");
    public int? UserId => HttpContext.Session.GetInt32("uid");
    public string? Role => HttpContext.Session.GetString("role");

    // Empty = public page. Otherwise only these roles may open it.
    protected virtual string[] AllowedRoles => Array.Empty<string>();

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        if (AllowedRoles.Length > 0 && (Role == null || !AllowedRoles.Contains(Role)))
        {
            if (Role == null)
            {
                string next = Request.Path + Request.QueryString;
                context.Result = RedirectToPage("/Login", new { next });
            }
            else
            {
                TempData["err"] = "You do not have access to that page.";
                context.Result = RedirectToPage("/Index");
            }
            return;
        }
        base.OnPageHandlerExecuting(context);
    }

    protected void Ok(string message) => TempData["ok"] = message;
    protected void Err(string message) => TempData["err"] = message;

    public static string Money(decimal v) => v.ToString("N2");

    // CSS class for a flight status badge, e.g. "on time" -> "status-on_time".
    public static string StatusClass(string status) => "status-" + status.Replace(' ', '_');
}
