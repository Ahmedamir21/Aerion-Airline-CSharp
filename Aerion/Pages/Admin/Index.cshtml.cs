namespace Aerion.Pages.Admin;

public class IndexModel : AppPage
{
    public IndexModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };
    public void OnGet() { }
}
