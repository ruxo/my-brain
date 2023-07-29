using Tirax.KMS.Migration.Utils;

namespace Tirax.KMS.Migration.Migrations;

public sealed class Initial : IMigration {
    public ValueTask Up() {
        throw new NotImplementedException();
    }

    public ValueTask Down() {
        throw new NotImplementedException();
    }

    public string Name => "Initial";
    public SemVer Version => new(0, 1, 0);
}