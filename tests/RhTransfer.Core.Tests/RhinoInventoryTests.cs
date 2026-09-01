using System.Collections.Generic;
using System.IO;
using System.Linq;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class RhinoInventoryTests
{

  private static IReadOnlyList<RhinoVersion> Installed(params int[] majors)
    => [.. majors.Select(m => new RhinoVersion(m))];

  private static RhinoInventory OnWindows(TempFolder temp, params int[] installed)
    => new(new RhinoRoot(temp.Path), () => Installed(installed), isWindows: true);

  private static RhinoInventory OnMac(TempFolder temp, params int[] installed)
    => new(new RhinoRoot(temp.Path), () => Installed(installed), isWindows: false);

  [Fact]
  public void InstalledWithSettingsIsConfigured()
  {
    using TempFolder temp = new();
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoInventory inventory = OnWindows(temp, 8);

    RhinoInstall install = Assert.Single(inventory.All());

    Assert.Equal(RhinoAvailability.Configured, install.Availability);
    Assert.True(install.CanExport);
  }

  /// <summary>
  /// The case that made a new machine unusable: Rhino is installed but has never been started,
  /// so it has no settings folder. It must still be offered as somewhere to import into.
  /// </summary>
  [Fact]
  public void InstalledWithoutSettingsCanStillBeImportedInto()
  {
    using TempFolder temp = new();

    RhinoInventory inventory = OnWindows(temp, 9);

    RhinoInstall install = Assert.Single(inventory.All());

    Assert.Equal(RhinoAvailability.InstalledWithoutSettings, install.Availability);
    Assert.False(install.CanExport);
    Assert.Empty(inventory.Exportable());
    Assert.Equal(Path.Combine(temp.Path, "9.0"), install.DataFolder.Path);
  }

  [Fact]
  public void SettingsWithoutTheApplicationAreStillListed()
  {
    using TempFolder temp = new();
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoInstall install = Assert.Single(OnMac(temp).All());

    Assert.Equal(RhinoAvailability.SettingsWithoutApplication, install.Availability);
    Assert.True(install.CanExport);
  }

  [Fact]
  public void MergesBothSourcesInVersionOrder()
  {
    using TempFolder temp = new();
    temp.File("7.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoInventory inventory = OnWindows(temp, 8, 9);

    Assert.Equal([7, 8, 9], inventory.All().Select(i => i.Version.Major));
    Assert.Equal([7, 8], inventory.Exportable().Select(i => i.Version.Major));

    Assert.Equal(RhinoAvailability.SettingsWithoutApplication, inventory.For(new RhinoVersion(7))!.Availability);
    Assert.Equal(RhinoAvailability.Configured, inventory.For(new RhinoVersion(8))!.Availability);
    Assert.Equal(RhinoAvailability.InstalledWithoutSettings, inventory.For(new RhinoVersion(9))!.Availability);
  }

  [Fact]
  public void NoRhinoAnywhereIsEmpty()
  {
    using TempFolder temp = new();

    Assert.Empty(new RhinoInventory(new RhinoRoot(temp.Path), () => [], isWindows: true).All());
  }

  private static RhsMetadata From(int major)
    => new(RhsMetadata.ToolName, "1.0.0", major, "win", System.DateTimeOffset.Now);

  /// <summary>
  /// A file exported from Rhino 8 should land in Rhino 8 by default, even when a newer Rhino is
  /// installed, because that is the import that needs no version warning.
  /// </summary>
  [Fact]
  public void DefaultsToTheVersionTheFileCameFrom()
  {
    using TempFolder temp = new();
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoInventory inventory = OnWindows(temp, 8, 9);

    Assert.Equal(8, inventory.PreferredTarget(From(8))!.Version.Major);
  }

  [Fact]
  public void DefaultsToTheNewestWhenThatVersionIsNotHere()
  {
    using TempFolder temp = new();

    RhinoInventory inventory = OnWindows(temp, 8, 9);

    Assert.Equal(9, inventory.PreferredTarget(From(7))!.Version.Major);
  }

  /// <summary>Rhino's own exports carry no provenance, so there is nothing to prefer.</summary>
  [Fact]
  public void DefaultsToTheNewestWhenTheFileSaysNothing()
  {
    using TempFolder temp = new();

    RhinoInventory inventory = OnWindows(temp, 7, 8, 9);

    Assert.Equal(9, inventory.PreferredTarget(null)!.Version.Major);
  }

  [Fact]
  public void NoRhinoMeansNoTarget()
  {
    using TempFolder temp = new();

    Assert.Null(new RhinoInventory(new RhinoRoot(temp.Path), () => [], isWindows: true).PreferredTarget(From(8)));
  }

  /// <summary>
  /// Rhino 7 predates the window layout format a .rhs is built around, so it can be exported
  /// from but never written into. On Windows it used the settings folder, so the export works.
  /// </summary>
  [Fact]
  public void Rhino7CanBeExportedFromOnWindowsButNotImportedInto()
  {
    using TempFolder temp = new();
    temp.File("7.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoInventory inventory = OnWindows(temp, 7, 8, 9);

    Assert.Equal([7, 8, 9], inventory.All().Select(i => i.Version.Major));
    Assert.Equal([7, 8], inventory.Exportable().Select(i => i.Version.Major));
    Assert.Equal([8, 9], inventory.ImportTargets().Select(i => i.Version.Major));
  }

  [Fact]
  public void AFileFromRhino7StillPrefersAVersionItCanBeWrittenInto()
  {
    using TempFolder temp = new();

    RhinoInventory inventory = OnWindows(temp, 7, 8, 9);

    Assert.Equal(9, inventory.PreferredTarget(From(7))!.Version.Major);
  }

  [Fact]
  public void OnlyRhino7MeansNowhereToImport()
  {
    using TempFolder temp = new();
    temp.File("7.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoInventory inventory = OnWindows(temp, 7);

    Assert.Single(inventory.All());
    Assert.Empty(inventory.ImportTargets());
    Assert.Null(inventory.PreferredTarget(null));
  }

  /// <summary>
  /// Rhino 7 on macOS kept its settings in its plist rather than the settings folder, so there
  /// is nothing there worth exporting. Rhino 9 draws the same line.
  /// </summary>
  [Fact]
  public void Rhino7CanOnlyBeExportedFromOnWindows()
  {
    using TempFolder temp = new();
    temp.File("7.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    Assert.Equal([7, 8], OnWindows(temp, 7, 8).Exportable().Select(i => i.Version.Major));
    Assert.Equal([8], OnMac(temp, 7, 8).Exportable().Select(i => i.Version.Major));

    // Either way it is listed, and either way it cannot be written into.
    Assert.Equal([7, 8], OnMac(temp, 7, 8).All().Select(i => i.Version.Major));
    Assert.Equal([8], OnMac(temp, 7, 8).ImportTargets().Select(i => i.Version.Major));
  }

}
