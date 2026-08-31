using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RhTransfer.Core.Application;

/// <summary>
/// Strips personal data from an exported settings file.
///
/// Rhino's licensing component writes the registration form straight into RhinoAppSettings
/// (Zoo/ValidationClient/Models/Registration.cs), which serialises to
/// settings/settings-Scheme__*.xml, so a plain export of a registered machine carries the
/// user's name, postal address, phone and email. Rhino 9 has no key-level filtering, only
/// path-level, so this has to happen here.
///
/// Only direct children of the root settings element are touched: a plug-in is free to have
/// its own key called "name" and must keep it.
/// </summary>
public static class IdentityScrubber
{

  private const string TelemetryPrefix = "Telemetry.";

  public static IReadOnlySet<string> Keys { get; } = new HashSet<string>(StringComparer.Ordinal)
  {
    "name",
    "email",
    "confirmEmail",
    "company",
    "address1",
    "address2",
    "city",
    "zip",
    "state",
    "phone",
    "country",
    "disciplines",
    "profession",
    "LicenseRefresh",
    "RecentFiles"
  };

  public static bool ShouldRemove(string key)
    => Keys.Contains(key) || key.StartsWith(TelemetryPrefix, StringComparison.Ordinal);

  /// <summary>
  /// Rewrites the file in place. Returns how many entries were removed, or null if the file
  /// could not be parsed as settings XML.
  /// </summary>
  public static int? Scrub(string settingsFilePath)
  {
    XDocument document;

    try
    {
      document = XDocument.Load(settingsFilePath, LoadOptions.PreserveWhitespace);
    }
    catch (System.Xml.XmlException)
    {
      return null;
    }
    catch (IOException)
    {
      return null;
    }

    XElement? inner = document.Root?.Element("settings");
    if (inner is null) return null;

    List<XElement> doomed = [.. inner
      .Elements("entry")
      .Where(e => e.Attribute("key")?.Value is string key && ShouldRemove(key))];

    if (doomed.Count == 0) return 0;

    foreach (XElement entry in doomed)
    {
      entry.Remove();
    }

    document.Save(settingsFilePath, SaveOptions.None);
    return doomed.Count;
  }

}
