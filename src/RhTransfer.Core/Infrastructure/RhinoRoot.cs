using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// The McNeel/Rhinoceros folder that holds one data folder per installed Rhino version,
/// the shared packages folder, and any restore points.
/// </summary>
public sealed class RhinoRoot
{

  private const string BackupMarker = "_backup_";

  public string Path { get; }

  public RhinoRoot(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      throw new ArgumentException("Root path must not be empty.", nameof(path));

    Path = path;
  }

  public static RhinoRoot ForCurrentUser() => new(DefaultPath());

  /// <summary>
  /// Where Rhino keeps per-user data. Mirrors CRhinoFileUtilities::GetRhinoRoamingProfileDataFolder.
  /// </summary>
  public static string DefaultPath()
  {
    if (OperatingSystem.IsWindows())
    {
      string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      return System.IO.Path.Combine(appData, "McNeel", "Rhinoceros");
    }

    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    if (OperatingSystem.IsMacOS())
      return System.IO.Path.Combine(home, "Library", "Application Support", "McNeel", "Rhinoceros");

    if (OperatingSystem.IsLinux())
      return System.IO.Path.Combine(home, ".local", "share", "mcneel", "rhinoceros");

    throw new PlatformNotSupportedException("Only Windows, macOS and Linux are supported.");
  }

  public bool Exists => Directory.Exists(Path);

  /// <summary>
  /// Data folders for every Rhino version present, oldest first.
  /// </summary>
  public IReadOnlyList<RhinoDataFolder> FindDataFolders()
  {
    if (!Directory.Exists(Path)) return [];

    List<RhinoDataFolder> folders = [];

    foreach (string directory in Directory.EnumerateDirectories(Path))
    {
      string name = new DirectoryInfo(directory).Name;
      if (!RhinoVersion.TryParseFolderName(name, out RhinoVersion version)) continue;
      if (!HoldsSettings(directory)) continue;

      folders.Add(new RhinoDataFolder(version, directory));
    }

    return [.. folders.OrderBy(f => f.Version)];
  }

  public RhinoDataFolder? FindDataFolder(RhinoVersion version)
    => FindDataFolders().FirstOrDefault(f => f.Version == version);

  /// <summary>
  /// Where a version's data would live, whether or not it is there yet. Importing onto a machine
  /// whose Rhino has never been started is the normal case, not an error.
  /// </summary>
  public RhinoDataFolder DataFolderFor(RhinoVersion version)
    => FindDataFolder(version) ?? new RhinoDataFolder(version, System.IO.Path.Combine(Path, version.FolderName));

  /// <summary>
  /// Restore points, newest first.
  /// </summary>
  public IReadOnlyList<BackupFolder> FindBackups()
  {
    if (!Directory.Exists(Path)) return [];

    List<BackupFolder> backups = [];

    foreach (string directory in Directory.EnumerateDirectories(Path))
    {
      string name = new DirectoryInfo(directory).Name;
      if (!name.Contains(BackupMarker, StringComparison.Ordinal)) continue;
      if (!HoldsSettings(directory)) continue;
      if (!TryParseBackupDate(name, out DateTime createdOn)) continue;

      backups.Add(new BackupFolder(directory, name, createdOn));
    }

    return [.. backups.OrderByDescending(b => b.CreatedOn).ThenByDescending(b => b.Name)];
  }

  public IReadOnlyList<BackupFolder> FindBackups(RhinoVersion version)
  {
    string prefix = version.FolderName + BackupMarker;
    return [.. FindBackups().Where(b => b.Name.StartsWith(prefix, StringComparison.Ordinal))];
  }

  /// <summary>
  /// A folder counts as real settings if it holds any settings XML or toolbar file.
  /// Licensing is ignored so that a licence-only folder does not look like a settings folder.
  /// </summary>
  public static bool HoldsSettings(string directory)
  {
    if (!Directory.Exists(directory)) return false;

    foreach (string child in Directory.EnumerateDirectories(directory))
    {
      string name = new DirectoryInfo(child).Name;
      if (string.Equals(name, "License Manager", StringComparison.OrdinalIgnoreCase)) continue;

      if (EnumerateSafely(child, "*.xml").Any()) return true;
      if (EnumerateSafely(child, "*.rui").Any()) return true;
    }

    return false;
  }

  public static bool TryParseBackupDate(string folderName, out DateTime createdOn)
  {
    createdOn = default;

    int index = folderName.IndexOf(BackupMarker, StringComparison.Ordinal);
    if (index < 0) return false;

    string stamp = folderName[(index + BackupMarker.Length)..];

    return DateTime.TryParseExact(
      stamp,
      "yyyy-MM-dd-HH-mm-ss",
      System.Globalization.CultureInfo.InvariantCulture,
      System.Globalization.DateTimeStyles.None,
      out createdOn);
  }

  public static string BackupName(RhinoVersion version, DateTime createdOn)
    => $"{version.FolderName}{BackupMarker}{createdOn:yyyy-MM-dd-HH-mm-ss}";

  private static IEnumerable<string> EnumerateSafely(string directory, string pattern)
  {
    try
    {
      return Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories);
    }
    catch (IOException)
    {
      return [];
    }
    catch (UnauthorizedAccessException)
    {
      return [];
    }
  }

}
