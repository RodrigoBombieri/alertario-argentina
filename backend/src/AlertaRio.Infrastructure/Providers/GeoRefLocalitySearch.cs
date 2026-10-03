using System.Globalization;
using System.Text;

namespace AlertaRio.Infrastructure.Providers;

// Searches a complete in-memory catalog; it does not associate localities with stations.
public sealed class GeoRefLocalitySearch
{
    private const int MaxQueryLength = 80;
    private const int MaxResults = 50;
    private readonly IReadOnlyList<Entry> entries;

    public GeoRefLocalitySearch(GeoRefCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Total <= 0 || snapshot.Localities.Count != snapshot.Total ||
            snapshot.Localities.Select(locality => locality.ExternalId)
                .Distinct(StringComparer.Ordinal).Count() != snapshot.Total)
            throw new ArgumentException("Search requires a complete GeoRef snapshot.",
                nameof(snapshot));

        entries = snapshot.Localities
            .Select(locality => new Entry(locality, Normalize(locality.Name),
                Normalize(locality.ProvinceName)))
            .OrderBy(entry => entry.NameKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProvinceKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.Locality.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Locality.ExternalId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<GeoRefLocalityCandidate> Search(
        string query, string? provinceId = null, int limit = 20)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length > MaxQueryLength)
            throw new ArgumentOutOfRangeException(nameof(query));
        if (limit is < 1 or > MaxResults)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (provinceId is not null && string.IsNullOrWhiteSpace(provinceId))
            throw new ArgumentException("Province ID must be nonempty.", nameof(provinceId));

        var key = Normalize(query);
        if (key.Length < 2) return [];
        var terms = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return entries
            .Where(entry => (provinceId is null ||
                    entry.Locality.ProvinceId == provinceId) &&
                terms.All(term => entry.NameKey.Contains(term, StringComparison.Ordinal)))
            .Take(limit)
            .Select(entry => entry.Locality)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        for (var index = 0; index < decomposed.Length; index++)
        {
            var character = decomposed[index];
            // Ñ is a distinct Spanish letter; other diacritics are search-insensitive.
            if (character is 'N' or 'n' && index + 1 < decomposed.Length &&
                decomposed[index + 1] == '\u0303')
            {
                result.Append('Ñ');
                index++;
                lastWasSpace = false;
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(character) is
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            {
                continue;
            }
            else if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToUpperInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                result.Append(' ');
                lastWasSpace = true;
            }
        }
        return result.ToString().TrimEnd();
    }

    private sealed record Entry(
        GeoRefLocalityCandidate Locality, string NameKey, string ProvinceKey);
}
