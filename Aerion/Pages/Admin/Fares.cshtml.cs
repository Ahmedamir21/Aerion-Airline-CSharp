namespace Aerion.Pages.Admin;

public class FaresModel : AppPage
{
    public FaresModel(Db db) : base(db) { }
    protected override string[] AllowedRoles => new[] { "admin" };

    public List<(int Id, string Name, decimal Price, decimal Miles)> Classes { get; set; } = new();

    public void OnGet()
    {
        Classes = Db.Query("SELECT FareClassID, ClassName, PriceMultiplier, MilesRate FROM FareClass", r => (r.Int("FareClassID"), r.Str("ClassName"), r.Dec("PriceMultiplier"), r.Dec("MilesRate")));
    }

    public IActionResult OnPost(int id, decimal miles)
    {
        if (miles <= 0 || miles > 10) Err("Miles multiplier must be between 0 and 10.");
        else { Db.Execute("UPDATE FareClass SET MilesRate = @m WHERE FareClassID = @i", ("@m", miles), ("@i", id)); Ok("Saved."); }
        return RedirectToPage();
    }
}
