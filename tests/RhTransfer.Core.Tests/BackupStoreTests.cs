using System;
using System.IO;
using System.Linq;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class BackupStoreTests
{

  [Fact]
  public void EmptyFolderNeedsNoRestorePoint()
  {
    using TempFolder temp = new();
    RhinoRoot root = new(temp.Path);
    RhinoDataFolder data = root.DataFolderFor(new RhinoVersion(8));

    BackupOutcome outcome = new BackupStore(root).Create(data, new TransferLog());

    Assert.IsType<BackupOutcome.NothingToBackUp>(outcome);
  }

  [Fact]
  public void RestorePointCopiesLicensingButNotCache()
  {
    using TempFolder temp = new();
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");
    temp.File("8.0/License Manager/licence.lic", "LICENCE");
    temp.File("8.0/Preview$Cache$/thumb.png", "cache");

    RhinoRoot root = new(temp.Path);
    RhinoDataFolder data = root.FindDataFolder(new RhinoVersion(8))!;

    BackupOutcome outcome = new BackupStore(root).Create(data, new TransferLog());

    BackupOutcome.Created created = Assert.IsType<BackupOutcome.Created>(outcome);
    Assert.True(File.Exists(Path.Combine(created.Folder.Path, "License Manager", "licence.lic")));
    Assert.False(Directory.Exists(Path.Combine(created.Folder.Path, "Preview$Cache$")));
  }

  [Fact]
  public void KeepsFourRestorePointsAndDropsTheOldest()
  {
    using TempFolder temp = new();
    temp.File("8.0/settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    RhinoRoot root = new(temp.Path);
    RhinoDataFolder data = root.FindDataFolder(new RhinoVersion(8))!;

    DateTime when = new(2026, 1, 1, 0, 0, 0);
    BackupStore store = new(root, () => when);
    TransferLog log = new();

    for (int i = 0; i < 6; i++)
    {
      when = when.AddMinutes(1);
      store.Create(data, log);
    }

    Assert.Equal(6, store.List(data.Version).Count);

    store.Prune(data.Version, log);

    Assert.Equal(BackupStore.MaximumPerVersion, store.List(data.Version).Count);
    Assert.Equal("8.0_backup_2026-01-01-00-06-00", store.List(data.Version).First().Name);
  }

}
