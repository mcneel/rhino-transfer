namespace RhTransfer.Core.Domain;

/// <summary>
/// Taking a restore point has three outcomes, not two: there may be nothing to protect yet,
/// which is the normal case on a machine whose Rhino has never been started.
/// </summary>
public abstract record BackupOutcome
{

  private BackupOutcome()
  {
  }

  public sealed record Created(BackupFolder Folder) : BackupOutcome;

  public sealed record NothingToBackUp : BackupOutcome;

  public sealed record Failed(string Reason) : BackupOutcome;

}
