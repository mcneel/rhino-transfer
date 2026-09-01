namespace RhTransfer.Core.Domain;

/// <summary>
/// A Rhino version this machine can receive settings for. Importing into a Rhino that has never
/// been started is the normal case on a new machine, so availability is not the same as having
/// a settings folder already.
/// </summary>
public sealed record RhinoInstall(RhinoDataFolder DataFolder, RhinoAvailability Availability)
{

  public RhinoVersion Version => DataFolder.Version;

  /// <summary>
  /// Set by RhinoInventory, which is the one place that knows both the platform and the version
  /// limits. Keeping them here rather than recomputing means the UI and the transfer cannot
  /// disagree about what is allowed.
  /// </summary>
  public required bool CanExport { get; init; }

  public required bool CanImport { get; init; }

  public override string ToString() => Availability switch
  {
    RhinoAvailability.Configured => $"Rhino {Version.Major}",
    RhinoAvailability.InstalledWithoutSettings => $"Rhino {Version.Major} (installed, not set up)",
    RhinoAvailability.SettingsWithoutApplication => $"Rhino {Version.Major} (settings only)"
  };

}
