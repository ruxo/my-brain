namespace Tirax.KMS.Migration.Core;

/// <summary>
/// Setup migration environment and run migration
/// </summary>
public interface IMigrationStage
{
    /// <summary>
    /// Run migration to the specified <paramref name="targetVersion"/>.
    /// </summary>
    /// <param name="targetVersion">Target version of the migration. <c>None</c> value for the latest.</param>
    ValueTask Run(Option<Version> targetVersion);
}