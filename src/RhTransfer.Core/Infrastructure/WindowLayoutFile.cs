using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// The window layout files that reference toolbar files by absolute path: containers.xml and
/// workspaces/*.xml inside each Scheme__* folder.
///
/// Rhino 9 discovers these references from its in-memory RuiCache, which needs a running Rhino.
/// We read them back off disk instead, which is what makes this work with Rhino closed.
///
/// Edits are a targeted replacement of the element's text rather than an XML round trip: these
/// files are stamped "DO NOT modify this file" and Rhino 9 rewrites them as text too, so
/// leaving every other byte alone is the conservative choice.
/// </summary>
public static class WindowLayoutFile
{

  private static Regex FileNameElement { get; } = new(
    @"(?<open><file_name\b[^>]*>)(?<path>[^<]*)(?<close></file_name>)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

  /// <summary>
  /// Every layout file under a settings folder, whether that folder is live or unpacked from a .rhs.
  /// </summary>
  public static IReadOnlyList<string> Find(string settingsFolder)
  {
    if (!Directory.Exists(settingsFolder)) return [];

    List<string> files = [];

    foreach (string scheme in Directory.EnumerateDirectories(settingsFolder, "Scheme__*"))
    {
      string containers = Path.Combine(scheme, "containers.xml");
      if (File.Exists(containers)) files.Add(containers);

      string workspaces = Path.Combine(scheme, "workspaces");
      if (!Directory.Exists(workspaces)) continue;

      files.AddRange(Directory.EnumerateFiles(workspaces, "*.xml"));
    }

    return files;
  }

  public static IReadOnlyList<string> ReadReferences(string layoutFilePath)
  {
    string text = File.ReadAllText(layoutFilePath);

    return [.. FileNameElement
      .Matches(text)
      .Select(m => Decode(m.Groups["path"].Value))
      .Where(p => !string.IsNullOrWhiteSpace(p))];
  }

  /// <summary>
  /// Rewrites each reference through <paramref name="map"/>. Returning the input unchanged
  /// leaves that reference alone. Returns how many were actually changed.
  /// </summary>
  public static int RewriteReferences(string layoutFilePath, Func<string, string> map)
  {
    string text = File.ReadAllText(layoutFilePath);
    int changed = 0;

    string rewritten = FileNameElement.Replace(text, match =>
    {
      string original = Decode(match.Groups["path"].Value);
      if (string.IsNullOrWhiteSpace(original)) return match.Value;

      string mapped = map(original);
      if (string.Equals(mapped, original, StringComparison.Ordinal)) return match.Value;

      changed++;
      return match.Groups["open"].Value + Encode(mapped) + match.Groups["close"].Value;
    });

    if (changed > 0) File.WriteAllText(layoutFilePath, rewritten);

    return changed;
  }

  private static string Decode(string value)
    => value
      .Replace("&lt;", "<", StringComparison.Ordinal)
      .Replace("&gt;", ">", StringComparison.Ordinal)
      .Replace("&quot;", "\"", StringComparison.Ordinal)
      .Replace("&apos;", "'", StringComparison.Ordinal)
      .Replace("&amp;", "&", StringComparison.Ordinal);

  private static string Encode(string value)
    => value
      .Replace("&", "&amp;", StringComparison.Ordinal)
      .Replace("<", "&lt;", StringComparison.Ordinal)
      .Replace(">", "&gt;", StringComparison.Ordinal);

}
