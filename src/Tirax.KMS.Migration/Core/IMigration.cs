namespace Tirax.KMS.Migration.Core;

public readonly record struct SemVer(int Major, int Minor, int Revision)
{
    sealed class SemVerComparer : IComparer<SemVer>
    {
        public int Compare(SemVer x, SemVer y) {
            var majorComparison = x.Major.CompareTo(y.Major);
            if (majorComparison != 0) return majorComparison;
            var minorComparison = x.Minor.CompareTo(y.Minor);
            return minorComparison != 0 ? minorComparison : x.Revision.CompareTo(y.Revision);
        }
    }

    public static readonly IComparer<SemVer> Comparer = new SemVerComparer();
}

public interface IMigration
{
    ValueTask Up();
    ValueTask Down();
    
    string Name { get; }
    Version Version { get; }
}