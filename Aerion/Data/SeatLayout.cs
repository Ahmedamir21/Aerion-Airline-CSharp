namespace Aerion.Data;

public class SeatRow
{
    public int Number { get; set; }
    public List<List<string>> Groups { get; set; } = new();
}

public class Cabin
{
    public string Name { get; set; } = "";
    public List<SeatRow> Rows { get; set; } = new();
}

// The aircraft only stores its seat count, so the cabin shape is derived from it:
// small planes 3-3, A330 size 2-4-2, the biggest 3-4-3. Front rows are business.
public static class SeatLayout
{
    private const string Letters = "ABCDEFGHJK";

    public static (int[] biz, int[] eco, int bizRows) Config(int total)
    {
        if (total >= 340) return (new[] { 1, 2, 1 }, new[] { 3, 4, 3 }, 6);
        if (total >= 280) return (new[] { 1, 2, 1 }, new[] { 2, 4, 2 }, 6);
        if (total >= 200) return (new[] { 2, 2 }, new[] { 3, 3 }, 3);
        return (new[] { 2, 2 }, new[] { 3, 3 }, 2);
    }

    public static List<Cabin> Build(int total, out List<int> exitRows)
    {
        var (bizGroups, ecoGroups, bizRows) = Config(total);
        int bizWidth = bizGroups.Sum();
        int ecoWidth = ecoGroups.Sum();
        int bizTotal = Math.Min(bizRows * bizWidth, total);
        int ecoTotal = total - bizTotal;

        var cabins = new List<Cabin>();
        int rowNo = 1;
        var specs = new[]
        {
            (name: "Business", groups: bizGroups, width: bizWidth, count: bizTotal),
            (name: "Economy", groups: ecoGroups, width: ecoWidth, count: ecoTotal)
        };
        foreach (var spec in specs)
        {
            var cabin = new Cabin { Name = spec.name };
            int left = spec.count;
            int rowCount = (int)Math.Ceiling(spec.count / (double)spec.width);
            for (int i = 0; i < rowCount; i++)
            {
                int n = Math.Min(spec.width, left);
                left -= n;
                var row = new SeatRow { Number = rowNo };
                int pos = 0;
                foreach (int g in spec.groups)
                {
                    var group = new List<string>();
                    for (int k = 0; k < g && pos < n; k++, pos++)
                    {
                        group.Add(rowNo.ToString() + Letters[pos]);
                    }
                    if (group.Count > 0) row.Groups.Add(group);
                }
                cabin.Rows.Add(row);
                rowNo++;
            }
            cabins.Add(cabin);
        }

        exitRows = new List<int>();
        var eco = cabins[1].Rows;
        if (eco.Count > 12)
        {
            exitRows.Add(eco[8].Number);
            exitRows.Add(eco[9].Number);
        }
        return cabins;
    }

    // Flat list used when a flight is created: (seat number, fare class name).
    public static List<(string Number, string Class)> SeatList(int total)
    {
        var list = new List<(string, string)>();
        foreach (var cabin in Build(total, out _))
            foreach (var row in cabin.Rows)
                foreach (var group in row.Groups)
                    foreach (var number in group)
                        list.Add((number, cabin.Name));
        return list;
    }
}
