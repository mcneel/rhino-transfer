using System;
using System.Collections.Generic;
using System.Linq;

using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

namespace RhTransfer.Core.Application;

/// <summary>
/// Every Rhino on this machine worth offering, whether it has settings yet or not.
///
/// Listing only settings folders is what made a new machine unusable: a Rhino that has just been
/// installed and never started has nothing under McNeel/Rhinoceros, yet it is precisely the one
/// people want to import into.
/// </summary>
public sealed class RhinoInventory
{

  private RhinoRoot Root { get; }

  /// <summary>
  /// Injected because it reads the machine's application folders, which a test cannot control.
  /// </summary>
  private Func<IReadOnlyList<RhinoVersion>> FindInstalled { get; }

  /// <summary>Injected so a test can ask what the answer would be on the other platform.</summary>
  private bool IsWindows { get; }

  public RhinoInventory(RhinoRoot root, Func<IReadOnlyList<RhinoVersion>>? findInstalled = null, bool? isWindows = null)
  {
    Root = root;
    FindInstalled = findInstalled ?? RhinoInstallLocator.FindInstalled;
    IsWindows = isWindows ?? OperatingSystem.IsWindows();
  }

  public IReadOnlyList<RhinoInstall> All()
  {
    Dictionary<RhinoVersion, RhinoDataFolder> withSettings = Root
      .FindDataFolders()
      .ToDictionary(f => f.Version);

    HashSet<RhinoVersion> installed = [.. FindInstalled()];

    List<RhinoInstall> installs = [];

    foreach (RhinoVersion version in withSettings.Keys.Concat(installed).Distinct())
    {
      bool hasSettings = withSettings.TryGetValue(version, out RhinoDataFolder? folder);

      RhinoAvailability availability = (hasSettings, installed.Contains(version)) switch
      {
        (true, true) => RhinoAvailability.Configured,
        (true, false) => RhinoAvailability.SettingsWithoutApplication,
        (false, _) => RhinoAvailability.InstalledWithoutSettings
      };

      bool hasSomethingToExport = availability is not RhinoAvailability.InstalledWithoutSettings;

      installs.Add(new RhinoInstall(folder ?? Root.DataFolderFor(version), availability)
      {
        CanExport = hasSomethingToExport && TransferLimits.CanExportFrom(version, IsWindows),
        CanImport = TransferLimits.CanImportInto(version)
      });
    }

    return [.. installs.OrderBy(i => i.Version)];
  }

  /// <summary>Versions there is actually something to export from.</summary>
  public IReadOnlyList<RhinoInstall> Exportable() => [.. All().Where(i => i.CanExport)];

  /// <summary>Versions a settings file may be written into. Rhino 7 is not one of them.</summary>
  public IReadOnlyList<RhinoInstall> ImportTargets() => [.. All().Where(i => i.CanImport)];

  public RhinoInstall? For(RhinoVersion version) => All().FirstOrDefault(i => i.Version == version);

  /// <summary>
  /// Where a settings file should go unless the user says otherwise: the version it came from,
  /// because that is the one that imports without a version warning. Falls back to the newest
  /// Rhino present when the file does not say, which Rhino's own exports do not.
  /// </summary>
  public RhinoInstall? PreferredTarget(RhsMetadata? metadata)
  {
    IReadOnlyList<RhinoInstall> installs = ImportTargets();
    if (installs.Count == 0) return null;

    if (metadata is not null)
    {
      RhinoInstall? match = installs.FirstOrDefault(i => i.Version.Major == metadata.SourceMajor);
      if (match is not null) return match;
    }

    return installs[^1];
  }

}
