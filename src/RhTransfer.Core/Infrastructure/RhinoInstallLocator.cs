using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// Which Rhino versions are installed, as opposed to which ones have settings. A Rhino that has
/// never been started has no settings folder, and that is exactly the machine people most want
/// to import into.
/// </summary>
public static class RhinoInstallLocator
{

  /// <summary>
  /// Rhino's bundle identifier is com.mcneel.rhinoceros.&lt;major&gt;. Reading it beats parsing the
  /// bundle name, which is "RhinoBETA.app" for prereleases and says nothing about the version.
  /// </summary>
  private static Regex MacBundleId { get; } = new(@"^com\.mcneel\.rhinoceros\.(\d+)$", RegexOptions.Compiled);

  /// <summary>Windows installs as "Rhino 8", and prereleases as "Rhino 9 WIP" or similar.</summary>
  private static Regex WindowsFolder { get; } = new(@"^Rhino\s+(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

  public static IReadOnlyList<RhinoVersion> FindInstalled()
  {
    if (OperatingSystem.IsMacOS()) return FindOnMac("/Applications");
    if (OperatingSystem.IsWindows()) return FindOnWindows();

    return [];
  }

  public static IReadOnlyList<RhinoVersion> FindOnMac(string applicationsFolder)
  {
    SortedSet<int> majors = [];

    foreach (string bundle in Directories(applicationsFolder))
    {
      string plist = Path.Combine(bundle, "Contents", "Info.plist");
      if (!File.Exists(plist)) continue;

      if (TryReadMacBundleMajor(plist, out int major)) majors.Add(major);
    }

    return [.. majors.Select(m => new RhinoVersion(m))];
  }

  /// <summary>
  /// Rhino's Info.plist is XML, so the identifier can be read without a binary plist parser.
  /// The key and its value are adjacent children of the same dict.
  /// </summary>
  public static bool TryReadMacBundleMajor(string plistPath, out int major)
  {
    major = 0;

    try
    {
      XDocument document = XDocument.Load(plistPath);

      foreach (XElement key in document.Descendants("key"))
      {
        if (key.Value != "CFBundleIdentifier") continue;
        if (key.NextNode is not XElement value) continue;

        Match match = MacBundleId.Match(value.Value.Trim());
        if (!match.Success) continue;

        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
    {
    }

    return false;
  }

  private static IReadOnlyList<RhinoVersion> FindOnWindows()
  {
    SortedSet<int> majors = [];

    string[] roots =
    [
      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
    ];

    foreach (string root in roots.Where(r => !string.IsNullOrEmpty(r)).Distinct())
    {
      foreach (RhinoVersion version in FindOnWindows(root))
      {
        majors.Add(version.Major);
      }
    }

    return [.. majors.Select(m => new RhinoVersion(m))];
  }

  /// <summary>
  /// A Rhino install is a "Rhino &lt;n&gt;" folder with System\Rhino.exe inside it. Requiring the
  /// executable keeps leftover empty folders from a previous uninstall out of the list.
  /// </summary>
  public static IReadOnlyList<RhinoVersion> FindOnWindows(string programFiles)
  {
    SortedSet<int> majors = [];

    foreach (string directory in Directories(programFiles))
    {
      Match match = WindowsFolder.Match(new DirectoryInfo(directory).Name);
      if (!match.Success) continue;

      if (!File.Exists(Path.Combine(directory, "System", "Rhino.exe"))) continue;
      if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int major)) continue;

      majors.Add(major);
    }

    return [.. majors.Select(m => new RhinoVersion(m))];
  }

  private static IEnumerable<string> Directories(string root)
  {
    if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return [];

    try
    {
      return Directory.EnumerateDirectories(root);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return [];
    }
  }

}
