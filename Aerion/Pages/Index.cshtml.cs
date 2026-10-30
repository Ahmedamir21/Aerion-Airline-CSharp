namespace Aerion.Pages;

public class IndexModel : AppPage
{
    public IndexModel(Db db) : base(db) { }

    public List<(string Code, string City)> Airports { get; private set; } = new();

    public void OnGet()
    {
        Airports = Db.Query("SELECT Code, City FROM Airport ORDER BY City", r => (r.Str("Code"), r.Str("City")));
    }
}
