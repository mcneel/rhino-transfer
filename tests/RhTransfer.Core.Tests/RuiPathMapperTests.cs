using System.IO;

using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class RuiPathMapperTests
{

  private static RhinoDataFolder DataFolder(TempFolder temp)
    => new(new RhinoVersion(8), Path.Combine(temp.Path, "8.0"));

  [Fact]
  public void DefaultToolbarBecomesUiRelative()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    RuiPathMapper mapper = new(data);

    string local = Path.Combine(data.UiPath, "default.rui");

    Assert.Equal(RuiLocation.DefaultUi, mapper.ClassifyLocal(local));
    Assert.Equal("UI/default.rui", mapper.ToArchivePath(local));
  }

  [Fact]
  public void ToolbarOutsideTheDataFolderBecomesExternal()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    RuiPathMapper mapper = new(data);

    string local = Path.Combine(temp.Path, "Desktop", "mine.rui");

    Assert.Equal(RuiLocation.External, mapper.ClassifyLocal(local));
    Assert.Equal("external/mine.rui", mapper.ToArchivePath(local));
  }

  [Fact]
  public void ImportedToolbarKeepsItsSchemeRelativePath()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    RuiPathMapper mapper = new(data);

    string local = Path.Combine(data.SettingsPath, "Scheme__Default", "imported_rui_files", "grabbed.rui");

    Assert.Equal(RuiLocation.ImportedRui, mapper.ClassifyLocal(local));
    Assert.Equal("Scheme__Default/imported_rui_files/grabbed.rui", mapper.ToArchivePath(local));
  }

  /// <summary>
  /// Rhino names a toolbar imported from a Windows .rhc after its whole original path, so on
  /// macOS the file name itself contains backslashes. Splitting on them would invent folders.
  /// </summary>
  [Fact]
  public void BackslashesInsideAFileNameAreNotSeparators()
  {
    if (Path.DirectorySeparatorChar != '/') return;

    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    RuiPathMapper mapper = new(data);

    string awkward = @"D:\Mijn GoogleDrive\SubD\2StandardErik8_2816464c.rui";
    string local = Path.Combine(data.SettingsPath, "Scheme__Default", "imported_rui_files", awkward);

    Assert.Equal(RuiLocation.ImportedRui, mapper.ClassifyLocal(local));
    Assert.Equal($"Scheme__Default/imported_rui_files/{awkward}", mapper.ToArchivePath(local));
  }

  [Fact]
  public void ArchivePathsComeBackAsLocalPaths()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    RuiPathMapper mapper = new(data);

    Assert.Equal(Path.Combine(data.UiPath, "default.rui"), mapper.ToLocalPath("UI/default.rui"));
    Assert.Equal(Path.Combine(data.Path, "external", "mine.rui"), mapper.ToLocalPath("external/mine.rui"));
  }

  /// <summary>
  /// The archive keeps the imported reference relative to the settings folder, while the file
  /// itself sits under settings/ in the archive. An export has to look in the right place to
  /// know it does not need to stage the file again.
  /// </summary>
  [Fact]
  public void ImportedToolbarEntrySitsUnderTheSettingsFolder()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    RuiPathMapper mapper = new(data);

    string imported = Path.Combine(data.SettingsPath, "Scheme__Default", "imported_rui_files", "grabbed.rui");

    Assert.Equal("settings/Scheme__Default/imported_rui_files/grabbed.rui", mapper.ToArchiveEntry(imported));
    Assert.Equal("UI/default.rui", mapper.ToArchiveEntry(Path.Combine(data.UiPath, "default.rui")));
  }

  /// <summary>
  /// A package toolbar reference resolves to the copy the export staged under external/ when the
  /// package itself is not installed here, and to the installed one when it is.
  /// </summary>
  [Fact]
  public void PackageToolbarFallsBackToTheStagedCopy()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    string staged = temp.File("8.0/external/Gone.rui", "toolbar");

    RuiPathMapper mapper = new(data);

    Assert.Equal(staged, mapper.ToLocalPath("packages/8.0/Gone/1.0/Gone.rui"));

    string installed = temp.File("packages/8.0/Gone/1.1/Gone.rui", "toolbar");

    Assert.Equal(installed, mapper.ToLocalPath("packages/8.0/Gone/1.0/Gone.rui"));
  }

  /// <summary>
  /// Rhino 9's own Windows exports leave absolute Windows paths in containers.xml. Importing one
  /// on another machine has to find the file by name or the toolbars come up empty.
  /// </summary>
  [Fact]
  public void ForeignWindowsPathResolvesToTheMatchingFileOnThisMachine()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    string expected = temp.File("8.0/UI/default.rui", "toolbar");

    RuiPathMapper mapper = new(data);

    string foreign = @"c:\users\bobi\appdata\roaming\mcneel\rhinoceros\9.0\ui\default.rui";

    Assert.Equal(expected, mapper.ToLocalPath(foreign));
  }

  [Fact]
  public void ForeignPathWithNoLocalMatchIsLeftAlone()
  {
    using TempFolder temp = new();
    RhinoDataFolder data = DataFolder(temp);
    temp.Folder("8.0/UI");

    RuiPathMapper mapper = new(data);

    string foreign = @"c:\users\bobi\nowhere\missing.rui";

    Assert.Equal(foreign, mapper.ToLocalPath(foreign));
  }

}
