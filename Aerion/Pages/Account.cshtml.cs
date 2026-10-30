namespace Aerion.Pages;

public class AccountModel : AppPage
{
    public AccountModel(Db db) : base(db) { }

    protected override string[] AllowedRoles => new[] { "passenger" };

    public int Balance { get; set; }
    public List<(DateTime When, int Miles, string Reason)> History { get; set; } = new();

    public void OnGet()
    {
        Balance = Db.Int("SELECT MilesBalance FROM LoyaltyAccount WHERE PassengerID = @p", ("@p", PassengerId));
        History = Db.Query(
            "SELECT TOP 30 lt.CreatedAt, lt.Miles, lt.Reason FROM LoyaltyTransaction lt JOIN LoyaltyAccount la ON la.LoyaltyID = lt.LoyaltyID " +
            "WHERE la.PassengerID = @p ORDER BY lt.CreatedAt DESC",
            r => (r.Date("CreatedAt"), r.Int("Miles"), r.Str("Reason")), ("@p", PassengerId));
    }
}
