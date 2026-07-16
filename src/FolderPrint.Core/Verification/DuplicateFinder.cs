using FolderPrint.Core.Models;

namespace FolderPrint.Core.Verification;

public sealed class DuplicateFinder
{
    private static readonly IComparer<IReadOnlyList<string>> OrdinalPathSequenceComparer =
        Comparer<IReadOnlyList<string>>.Create(ComparePathSequences);

    public IReadOnlyList<IReadOnlyList<string>> Find(FolderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Files.GroupBy(file => file.Sha256, StringComparer.Ordinal)
            .Select(group => new
            {
                FingerprintCount = group.Count(),
                Paths = group.Select(file => file.RelativePath)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
            })
            .Where(group => group.FingerprintCount >= 2)
            .OrderBy(group => (IReadOnlyList<string>)group.Paths, OrdinalPathSequenceComparer)
            .Select(group => (IReadOnlyList<string>)group.Paths)
            .ToArray();
    }

    private static int ComparePathSequences(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
        {
            var comparison = StringComparer.Ordinal.Compare(left[index], right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Count.CompareTo(right.Count);
    }
}
