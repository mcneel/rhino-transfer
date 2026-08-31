using System.IO;

namespace RhTransfer.Core.Domain;

/// <summary>
/// A per-version Rhino data folder that has been found on disk and looks like real settings.
/// </summary>
public sealed record RhinoDataFolder(RhinoVersion Version, string Path)
{

  public string SettingsPath => System.IO.Path.Combine(Path, "settings");

  public string UiPath => System.IO.Path.Combine(Path, "UI");

  public string PlugInsPath => System.IO.Path.Combine(Path, "Plug-ins");

  /// <summary>
  /// Yak packages live beside the version folders, not inside one.
  /// </summary>
  public string PackagesPath
  {
    get
    {
      string? root = Directory.GetParent(Path)?.FullName;
      return root is null ? System.IO.Path.Combine(Path, "packages") : System.IO.Path.Combine(root, "packages");
    }
  }

  public override string ToString() => $"{Version} ({Path})";

}
