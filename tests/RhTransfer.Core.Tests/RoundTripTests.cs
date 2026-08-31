using System;
using System.IO;
using System.Linq;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

/// <summary>
/// Export a settings folder, import it somewhere else, and check the toolbars still resolve.
/// This is the whole product in one test.
/// </summary>
public sealed class RoundTripTests
{

  private const string Containers = """
    <?xml version="1.0" encoding="utf-8"?>
    <RhinoUI major_ver="1" minor_ver="0" name="Default Window Layout">
      <files>
        <file_name guid="c7da84fc" source="Assembly">{0}</file_name>
        <file_name guid="2531e4e5" source="File">{1}</file_name>
      </files>
    </RhinoUI>
    """;

  private static string BuildSourceFolder(TempFolder temp, out string customToolbar)
  {
    string data = temp.Folder("source/8.0");

    temp.File("source/8.0/UI/default.rui", "default toolbar");
    customToolbar = temp.File("elsewhere/mine.rui", "my toolbar");

    temp.File("source/8.0/settings/settings-Scheme__Default.xml", """
      <?xml version="1.0" encoding="utf-8"?>
      <settings id="2.0">
        <settings>
          <entry key="email">someone@example.com</entry>
          <entry key="gradientHatch_enabled">False</entry>
        </settings>
      </settings>
      """);

    temp.File("source/8.0/settings/Scheme__Default/containers.xml", string.Format(
      Containers,
      Path.Combine(data, "UI", "default.rui"),
      customToolbar));

    temp.File("source/8.0/License Manager/licence.lic", "SECRET LICENCE");
    temp.File("source/8.0/Render Content/en_US/Textures/big.exr", new string('x', 1000));

    return data;
  }

  [Fact]
  public void SettingsSurviveTheTripAndToolbarsStillResolve()
  {
    using TempFolder temp = new();

    string sourcePath = BuildSourceFolder(temp, out string customToolbar);
    RhinoDataFolder source = new(new RhinoVersion(8), sourcePath);

    string archive = Path.Combine(temp.Path, "settings_8.rhs");

    TransferReport exported = new SettingsExporter("1.0.0").Export(source, archive);
    Assert.True(exported.IsSuccess, string.Join("; ", exported.Errors.Select(e => e.Text)));

    RhinoRoot targetRoot = new(temp.Folder("target"));
    RhinoDataFolder target = targetRoot.DataFolderFor(new RhinoVersion(8));

    TransferReport imported = new SettingsImporter(new BackupStore(targetRoot)).Import(archive, target);
    Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(e => e.Text)));

    Assert.True(File.Exists(Path.Combine(target.SettingsPath, "settings-Scheme__Default.xml")));
    Assert.True(File.Exists(Path.Combine(target.UiPath, "default.rui")));
    Assert.True(File.Exists(Path.Combine(target.Path, "external", "mine.rui")));

    string containers = Path.Combine(target.SettingsPath, "Scheme__Default", "containers.xml");

    foreach (string reference in WindowLayoutFile.ReadReferences(containers))
    {
      Assert.True(File.Exists(reference), $"toolbar reference does not resolve: {reference}");
    }
  }

  [Fact]
  public void LicensingNeverEntersTheArchive()
  {
    using TempFolder temp = new();

    string sourcePath = BuildSourceFolder(temp, out _);
    RhinoDataFolder source = new(new RhinoVersion(8), sourcePath);

    string archive = Path.Combine(temp.Path, "settings_8.rhs");
    new SettingsExporter("1.0.0").Export(source, archive);

    string extracted = temp.Folder("peek");
    RhsArchive.Extract(archive, extracted, new TransferLog());

    Assert.False(Directory.Exists(Path.Combine(extracted, "License Manager")));
    Assert.False(Directory.Exists(Path.Combine(extracted, "Render Content")));
  }

  [Fact]
  public void PersonalDataNeverEntersTheArchive()
  {
    using TempFolder temp = new();

    string sourcePath = BuildSourceFolder(temp, out _);
    RhinoDataFolder source = new(new RhinoVersion(8), sourcePath);

    string archive = Path.Combine(temp.Path, "settings_8.rhs");
    new SettingsExporter("1.0.0").Export(source, archive);

    string extracted = temp.Folder("peek");
    RhsArchive.Extract(archive, extracted, new TransferLog());

    string settings = File.ReadAllText(Path.Combine(extracted, "settings", "settings-Scheme__Default.xml"));

    Assert.DoesNotContain("someone@example.com", settings);
    Assert.Contains("gradientHatch_enabled", settings);
  }

  /// <summary>
  /// An import over existing settings must be undoable, and the licence must survive it.
  /// </summary>
  [Fact]
  public void ImportBacksUpWhatItReplacesAndKeepsTheLicence()
  {
    using TempFolder temp = new();

    string sourcePath = BuildSourceFolder(temp, out _);
    RhinoDataFolder source = new(new RhinoVersion(8), sourcePath);

    string archive = Path.Combine(temp.Path, "settings_8.rhs");
    new SettingsExporter("1.0.0").Export(source, archive);

    RhinoRoot targetRoot = new(temp.Folder("target"));
    temp.File("target/8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\"><settings /></settings>");
    temp.File("target/8.0/License Manager/licence.lic", "TARGET LICENCE");

    RhinoDataFolder target = targetRoot.DataFolderFor(new RhinoVersion(8));
    TransferReport report = new SettingsImporter(new BackupStore(targetRoot)).Import(archive, target);

    Assert.True(report.IsSuccess);
    Assert.NotNull(report.BackupPath);
    Assert.True(Directory.Exists(report.BackupPath));

    string licence = Path.Combine(target.Path, "License Manager", "licence.lic");
    Assert.True(File.Exists(licence));
    Assert.Equal("TARGET LICENCE", File.ReadAllText(licence));
  }

  [Fact]
  public void ArchiveCarriesItsProvenance()
  {
    using TempFolder temp = new();

    string sourcePath = BuildSourceFolder(temp, out _);
    RhinoDataFolder source = new(new RhinoVersion(8), sourcePath);

    string archive = Path.Combine(temp.Path, "settings_8.rhs");
    new SettingsExporter("2.3.4").Export(source, archive);

    RhsMetadata? metadata = RhsArchive.ReadMetadata(archive);

    Assert.NotNull(metadata);
    Assert.Equal(8, metadata.SourceMajor);
    Assert.Equal("2.3.4", metadata.ToolVersion);
    Assert.Equal(RhsMetadata.ToolName, metadata.Tool);
  }

  /// <summary>
  /// A package toolbar is referenced but never carried, the same as Rhino 9: the package brings
  /// it back when it is installed. Importing without the package must say so, not fail silently.
  /// </summary>
  [Fact]
  public void MissingPackageToolbarIsReported()
  {
    using TempFolder temp = new();

    string sourceRoot = temp.Folder("source");
    string packageToolbar = temp.File("source/packages/8.0/Gone/1.0/Gone.rui", "toolbar");

    temp.File("source/8.0/settings/settings-Scheme__Default.xml", """
      <settings id="2.0"><settings /></settings>
      """);

    temp.File("source/8.0/settings/Scheme__Default/containers.xml",
      $"""<files><file_name source="PlugInFolder">{packageToolbar}</file_name></files>""");

    RhinoDataFolder source = new(new RhinoVersion(8), Path.Combine(sourceRoot, "8.0"));

    string archive = Path.Combine(temp.Path, "settings_8.rhs");
    Assert.True(new SettingsExporter("1.0.0").Export(source, archive).IsSuccess);

    string peek = temp.Folder("peek");
    RhsArchive.Extract(archive, peek, new TransferLog());

    string exportedReference = WindowLayoutFile
      .ReadReferences(Path.Combine(peek, "settings", "Scheme__Default", "containers.xml"))
      .Single();

    Assert.Equal("packages/8.0/Gone/1.0/Gone.rui", exportedReference);
    Assert.False(File.Exists(Path.Combine(peek, "packages", "8.0", "Gone", "1.0", "Gone.rui")));

    RhinoRoot targetRoot = new(temp.Folder("target"));
    RhinoDataFolder target = targetRoot.DataFolderFor(new RhinoVersion(8));

    TransferReport report = new SettingsImporter(new BackupStore(targetRoot)).Import(archive, target);

    Assert.True(report.IsSuccess);
    Assert.Contains(report.Warnings, w => w.Text.Contains("Gone.rui") && w.Text.Contains("package"));
  }

  /// <summary>
  /// The same reference resolves silently when the package is installed on the target.
  /// </summary>
  [Fact]
  public void PackageToolbarRelinksWhenThePackageIsThere()
  {
    using TempFolder temp = new();

    string sourceRoot = temp.Folder("source");
    string packageToolbar = temp.File("source/packages/8.0/Gone/1.0/Gone.rui", "toolbar");

    temp.File("source/8.0/settings/settings-Scheme__Default.xml", """
      <settings id="2.0"><settings /></settings>
      """);

    temp.File("source/8.0/settings/Scheme__Default/containers.xml",
      $"""<files><file_name source="PlugInFolder">{packageToolbar}</file_name></files>""");

    RhinoDataFolder source = new(new RhinoVersion(8), Path.Combine(sourceRoot, "8.0"));

    string archive = Path.Combine(temp.Path, "settings_8.rhs");
    new SettingsExporter("1.0.0").Export(source, archive);

    RhinoRoot targetRoot = new(temp.Folder("target"));
    string installed = temp.File("target/packages/8.0/Gone/1.0/Gone.rui", "toolbar");
    RhinoDataFolder target = targetRoot.DataFolderFor(new RhinoVersion(8));

    TransferReport report = new SettingsImporter(new BackupStore(targetRoot)).Import(archive, target);

    Assert.True(report.IsSuccess);
    Assert.Empty(report.Warnings);

    string reference = WindowLayoutFile
      .ReadReferences(Path.Combine(target.SettingsPath, "Scheme__Default", "containers.xml"))
      .Single();

    Assert.Equal(installed, reference);
  }

}
