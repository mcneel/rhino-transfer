using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class RhinoRootTests
{

  private static RhinoRoot Populated(TempFolder temp)
  {
    temp.File("7.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("9.0/UI/default.rui", "toolbar");

    temp.Folder("10.0");                                     // present but empty
    temp.File("packages/8.0/thing/1.0/thing.rui", "pkg");    // not a version folder
    temp.File("notaversion/settings/x.xml", "<x />");

    return new RhinoRoot(temp.Path);
  }

  [Fact]
  public void FindsOnlyVersionFoldersThatHoldSettings()
  {
    using TempFolder temp = new();
    RhinoRoot root = Populated(temp);

    int[] found = [.. root.FindDataFolders().Select(f => f.Version.Major)];

    Assert.Equal([7, 8, 9], found);
  }

  [Fact]
  public void NamesWhereAVersionWouldLiveEvenIfItIsNotThere()
  {
    using TempFolder temp = new();
    RhinoRoot root = Populated(temp);

    RhinoDataFolder folder = root.DataFolderFor(new RhinoVersion(11));

    Assert.Equal(Path.Combine(temp.Path, "11.0"), folder.Path);
    Assert.False(Directory.Exists(folder.Path));
  }

  [Fact]
  public void PackagesLiveBesideTheVersionFolders()
  {
    using TempFolder temp = new();
    RhinoRoot root = Populated(temp);

    RhinoDataFolder eight = root.FindDataFolder(new RhinoVersion(8))!;

    Assert.Equal(Path.Combine(temp.Path, "packages"), eight.PackagesPath);
  }

  /// <summary>
  /// The naming has to match Rhino 9's so its Migration Assistant lists our restore points.
  /// </summary>
  [Fact]
  public void BackupNamingMatchesRhino9()
  {
    DateTime when = new(2026, 8, 31, 9, 3, 21);

    string name = RhinoRoot.BackupName(new RhinoVersion(8), when);

    Assert.Equal("8.0_backup_2026-08-31-09-03-21", name);
    Assert.True(RhinoRoot.TryParseBackupDate(name, out DateTime parsed));
    Assert.Equal(when, parsed);
  }

  [Fact]
  public void ReadsRhino9sOwnBackupFolders()
  {
    using TempFolder temp = new();
    temp.File("9.0_backup_2026-08-26-23-05-01/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("9.0_backup_2026-08-31-09-03-21/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("8.0_backup_2026-01-01-00-00-00/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoRoot root = new(temp.Path);

    IReadOnlyList<BackupFolder> nine = root.FindBackups(new RhinoVersion(9));

    Assert.Equal(2, nine.Count);
    Assert.Equal("9.0_backup_2026-08-31-09-03-21", nine[0].Name);
    Assert.Single(root.FindBackups(new RhinoVersion(8)));
  }

  [Fact]
  public void MissingRootIsEmptyRatherThanAnError()
  {
    RhinoRoot root = new(Path.Combine(Path.GetTempPath(), "rh-transfer-nope", Guid.NewGuid().ToString("n")));

    Assert.False(root.Exists);
    Assert.Empty(root.FindDataFolders());
    Assert.Empty(root.FindBackups());
  }

}
