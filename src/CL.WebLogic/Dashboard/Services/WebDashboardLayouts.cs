using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CL.WebLogic.Dashboard.Models;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>Pure helpers for dashboard layouts: keys, ordering, moving, adding and removing items.</summary>
public static partial class WebDashboardLayouts
{
    /// <summary>Maximum items on one dashboard.</summary>
    public const int MaxItems = 100;

    /// <summary>True for a valid dashboard or owner key (<c>^[A-Za-z0-9_.:-]{1,128}$</c>, no <c>..</c>).</summary>
    public static bool IsValidKey(string? key) => key is not null && Key().IsMatch(key) && !key.Contains("..", StringComparison.Ordinal);

    /// <summary>True for a valid zone name.</summary>
    public static bool IsValidZone(string? zone) => zone is not null && Zone().IsMatch(zone);

    /// <summary>A new instance id.</summary>
    public static string NewInstanceId() => "w" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));

    /// <summary>
    /// Sorts by zone (in <paramref name="zoneOrder"/> order, unknown zones after) then order, numbers
    /// each zone 0..n, fills in missing instance ids and drops duplicate instance ids.
    /// </summary>
    public static IReadOnlyList<WebDashboardItem> Normalize(IEnumerable<WebDashboardItem> items, IReadOnlyList<string>? zoneOrder = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var zones = zoneOrder ?? [];
        var result = new List<WebDashboardItem>();
        var index = 0;
        foreach (var group in items
            .Select((item, position) => (Item: item with { InstanceId = string.IsNullOrEmpty(item.InstanceId) ? NewInstanceId() : item.InstanceId }, Position: position))
            .Where(x => seen.Add(x.Item.InstanceId))
            .GroupBy(x => x.Item.Zone, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => ZoneRank(zones, g.Key)).ThenBy(g => g.Min(x => x.Position)))
        {
            index = 0;
            foreach (var (item, _) in group.OrderBy(x => x.Item.Order).ThenBy(x => x.Position))
                result.Add(item with { Order = index++ });
        }
        return result;
    }

    /// <summary>
    /// The id of a default item that was not saved yet: derived from the owner, dashboard, position
    /// and widget, so it is the same on every render and survives the first save.
    /// </summary>
    public static string DefaultInstanceId(string ownerKey, string dashboardKey, int position, string widgetName)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', ownerKey, dashboardKey, position.ToString(System.Globalization.CultureInfo.InvariantCulture), widgetName));
        return "d" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input))[..16];
    }

    /// <summary>Moves an item to <paramref name="zone"/> at <paramref name="index"/> (clamped).</summary>
    public static IReadOnlyList<WebDashboardItem> Move(IReadOnlyList<WebDashboardItem> items, string instanceId, string zone, int index)
    {
        ArgumentNullException.ThrowIfNull(items);
        var item = items.FirstOrDefault(i => i.InstanceId == instanceId) ?? throw new ArgumentException($"No item '{instanceId}'.", nameof(instanceId));
        var others = items.Where(i => i.InstanceId != instanceId).ToList();
        var target = others.Where(i => string.Equals(i.Zone, zone, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Order).ToList();
        target.Insert(Math.Clamp(index, 0, target.Count), item with { Zone = zone });
        var renumbered = target.Select((i, position) => i with { Order = position });
        return Normalize(others.Where(i => !string.Equals(i.Zone, zone, StringComparison.OrdinalIgnoreCase)).Concat(renumbered));
    }

    /// <summary>Adds a widget at the end of <paramref name="zone"/>.</summary>
    public static IReadOnlyList<WebDashboardItem> Add(IReadOnlyList<WebDashboardItem> items, string widgetName, string zone = "main", IReadOnlyDictionary<string, string>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count >= MaxItems)
            throw new InvalidOperationException($"A dashboard holds at most {MaxItems} items.");
        var order = items.Where(i => string.Equals(i.Zone, zone, StringComparison.OrdinalIgnoreCase)).Select(i => i.Order + 1).DefaultIfEmpty(0).Max();
        return Normalize([.. items, new WebDashboardItem { WidgetName = widgetName, Zone = zone, Order = order, Parameters = parameters ?? new Dictionary<string, string>() }]);
    }

    /// <summary>Removes an item.</summary>
    public static IReadOnlyList<WebDashboardItem> Remove(IReadOnlyList<WebDashboardItem> items, string instanceId) =>
        Normalize((items ?? throw new ArgumentNullException(nameof(items))).Where(i => i.InstanceId != instanceId));

    private static int ZoneRank(IReadOnlyList<string> zones, string zone)
    {
        for (var i = 0; i < zones.Count; i++)
        {
            if (string.Equals(zones[i], zone, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return zones.Count;
    }

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,128}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Key();

    [GeneratedRegex("^[a-z0-9_-]{1,32}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Zone();
}
