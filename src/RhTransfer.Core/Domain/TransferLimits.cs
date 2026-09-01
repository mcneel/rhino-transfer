namespace RhTransfer.Core.Domain;

/// <summary>
/// Which Rhino versions a settings file can come from and go to. Both limits are format
/// boundaries rather than policy, so they live together instead of being spread across the
/// front ends.
/// </summary>
public static class TransferLimits
{

  /// <summary>
  /// A .rhs is built around the window layout format (containers.xml and workspaces) that
  /// arrived in Rhino 8, so Rhino 7 cannot be written into on any platform.
  /// </summary>
  public static RhinoVersion EarliestImportTarget { get; } = new(8);

  /// <summary>
  /// Rhino 7 on macOS kept its settings in ~/Library/Preferences/com.mcneel.rhinoceros.7.plist
  /// rather than the settings folder, so there is next to nothing in 7.0 worth carrying and an
  /// export would quietly produce an empty setup. Rhino 7 on Windows used the folder and is
  /// fine. Rhino 9 draws the same line: EARLEST_VERSION is 8 on macOS and 7 on Windows.
  /// </summary>
  public static RhinoVersion EarliestExportSource(bool isWindows) => new(isWindows ? 7 : 8);

  public static bool CanImportInto(RhinoVersion version)
    => version.Major >= EarliestImportTarget.Major;

  public static bool CanExportFrom(RhinoVersion version, bool isWindows)
    => version.Major >= EarliestExportSource(isWindows).Major;

}
