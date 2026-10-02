namespace AlertaRio.Infrastructure.Providers;

public sealed record GeoRefCatalogSnapshot(
    int Total, IReadOnlyList<GeoRefLocalityCandidate> Localities);

// Callers replace an existing snapshot only after this complete candidate has been built.
public static class GeoRefSnapshotBuilder
{
    public static GeoRefCatalogSnapshot Build(IEnumerable<GeoRefLocalityPage> pages)
    {
        var ordered = pages.OrderBy(page => page.Start).ToArray();
        if (ordered.Length == 0 || ordered[0].Total == 0)
            throw new InvalidDataException("An empty GeoRef response cannot replace a catalog.");

        var total = ordered[0].Total;
        long nextStart = 0;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var localities = new List<GeoRefLocalityCandidate>();
        foreach (var page in ordered)
        {
            if (page.Total != total || page.Count <= 0 ||
                page.Candidates.Count != page.Count || page.Start != nextStart ||
                nextStart + page.Count > total)
                throw new InvalidDataException("GeoRef catalog pages are incomplete or inconsistent.");
            foreach (var locality in page.Candidates)
            {
                if (!identities.Add(locality.ExternalId))
                    throw new InvalidDataException("GeoRef catalog repeats a locality ID.");
                localities.Add(locality);
            }
            nextStart += page.Count;
        }

        if (nextStart != total)
            throw new InvalidDataException("GeoRef catalog is missing pages.");
        return new GeoRefCatalogSnapshot(total, localities.AsReadOnly());
    }
}
