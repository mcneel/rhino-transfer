using System;

namespace RhTransfer.Core.Domain;

/// <summary>
/// A Rhino major version. Rhino stores per-user data in a folder named "&lt;major&gt;.0",
/// so the minor part is never part of the identity.
/// </summary>
public readonly record struct RhinoVersion : IComparable<RhinoVersion>
{

  public int Major { get; }

  public RhinoVersion(int major)
  {
    if (major < 1 || major > 99)
      throw new ArgumentOutOfRangeException(nameof(major), major, "Rhino major version must be between 1 and 99.");

    Major = major;
  }

  public string FolderName => $"{Major}.0";

  public static bool TryParseFolderName(string folderName, out RhinoVersion version)
  {
    version = default;

    if (!Version.TryParse(folderName, out Version? parsed)) return false;
    if (parsed.Major < 1 || parsed.Major > 99) return false;

    version = new RhinoVersion(parsed.Major);
    return true;
  }

  public int CompareTo(RhinoVersion other) => Major.CompareTo(other.Major);

  public override string ToString() => $"Rhino {Major}";

}
