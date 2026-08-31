using System;
using System.Collections.Generic;

namespace RhTransfer.Core.Application;

/// <summary>
/// What goes into an archive, what never leaves the machine, and what an import must not delete.
///
/// Inclusion is a whitelist, matching Rhino 9: its exporter walks a fixed list of bundles
/// (OptionsExportViewModel.cs) rather than sweeping the data folder, which is why a real
/// OptionsExport archive contains no Render Content, no caches and no licensing.
///
/// <see cref="Licensing"/> is Rhino 9's own ignore list, added for RH-78734
/// ("Reset LEAKS License information"): anything that authorises a seat.
/// </summary>
public static class ExportPolicy
{

  /// <summary>Top-level folders of the data directory that carry settings worth moving.</summary>
  public static IReadOnlyList<string> IncludedRoots { get; } =
  [
    "settings",
    "UI",
    "Plug-ins",
    "scripts",
    "Localization"
  ];

  public static IReadOnlySet<string> Licensing { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  {
    "License Manager",
    "keypairs",
    "ra_cache.txt",
    "proxy.txt"
  };

  public static IReadOnlySet<string> Cache { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  {
    ".DS_Store",
    "EBWebView",
    "Preview$Cache$",
    "Texture$Cache$",
    "NamedItem$Cache$",
    "DynamicIcon$Cache$",
    "compat_cache_netcore"
  };

  /// <summary>The RhinoCycles device cache, a folder of empty marker files.</summary>
  private static IReadOnlyList<string> CacheFragments { get; } = ["/data/gpus/"];

  /// <summary>
  /// Localization holds a lot of shipped content; only the user's own colour overrides matter.
  /// </summary>
  private const string ColoursFile = "/support/colors.txt";

  public static bool ShouldExport(string relativePath)
  {
    if (string.IsNullOrEmpty(relativePath)) return false;

    string normalised = relativePath.Replace('\\', '/');

    if (!IsUnderIncludedRoot(normalised)) return false;
    if (IsCache(normalised)) return false;
    if (MatchesSegment(normalised, Licensing)) return false;

    if (normalised.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase))
      return normalised.EndsWith(ColoursFile, StringComparison.OrdinalIgnoreCase);

    return true;
  }

  /// <summary>
  /// A restore point keeps licensing, because it has to put the machine back exactly as it was.
  /// Cache is still skipped: it costs disk and regenerates itself.
  /// </summary>
  public static bool ShouldBackUp(string relativePath)
    => !string.IsNullOrEmpty(relativePath) && !IsCache(relativePath.Replace('\\', '/'));

  /// <summary>Left in place when an import clears the target folder.</summary>
  public static bool IsPreservedOnImport(string name) => Licensing.Contains(name);

  private static bool IsUnderIncludedRoot(string normalised)
  {
    foreach (string root in IncludedRoots)
    {
      if (normalised.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) return true;
    }

    return false;
  }

  private static bool IsCache(string normalised)
  {
    if (MatchesSegment(normalised, Cache)) return true;

    foreach (string fragment in CacheFragments)
    {
      if (normalised.Contains(fragment, StringComparison.OrdinalIgnoreCase)) return true;
    }

    return false;
  }

  private static bool MatchesSegment(string normalised, IReadOnlySet<string> segments)
  {
    foreach (string segment in normalised.Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
      if (segments.Contains(segment)) return true;
    }

    return false;
  }

}
