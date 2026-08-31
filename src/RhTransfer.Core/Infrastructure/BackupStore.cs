using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// Restore points beside the data folder. The naming and the cap of four match Rhino 9
/// (TransferSettingsUtilities.CreateRestorePointName / MAXIMUM_BACKUP_COUNT) so that backups
/// made here show up in Rhino 9's Migration Assistant and vice versa.
/// </summary>
public sealed class BackupStore
{

  public const int MaximumPerVersion = 4;

  private RhinoRoot Root { get; }

  private Func<DateTime> Clock { get; }

  public BackupStore(RhinoRoot root, Func<DateTime>? clock = null)
  {
    Root = root;
    Clock = clock ?? (() => DateTime.Now);
  }

  public IReadOnlyList<BackupFolder> List(RhinoVersion version) => Root.FindBackups(version);

  /// <summary>
  /// Clones the data folder to a timestamped sibling.
  /// </summary>
  public BackupOutcome Create(RhinoDataFolder data, TransferLog log)
  {
    if (!HasAnything(data.Path))
    {
      log.Info("nothing to back up, these settings are new");
      return new BackupOutcome.NothingToBackUp();
    }

    DateTime now = Clock();
    string name = RhinoRoot.BackupName(data.Version, now);
    string path = Path.Combine(Root.Path, name);

    if (Directory.Exists(path))
    {
      log.Warn($"a restore point named {name} already exists, reusing it");
    }

    CopyResult result = DirectoryCopier.Copy(data.Path, path, ExportPolicy.ShouldBackUp, log);

    if (result.FileCount == 0)
      return new BackupOutcome.Failed($"could not create a restore point at {path}");

    log.Info($"restore point: {name} ({result.FileCount} files)");
    return new BackupOutcome.Created(new BackupFolder(path, name, now));
  }

  private static bool HasAnything(string folder)
  {
    if (!Directory.Exists(folder)) return false;

    try
    {
      return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any();
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return true;
    }
  }

  /// <summary>
  /// Copies a restore point back over the data folder. Used to undo a failed import.
  /// </summary>
  public bool Restore(BackupFolder backup, RhinoDataFolder data, TransferLog log)
  {
    if (!Directory.Exists(backup.Path))
    {
      log.Error($"restore point {backup.Name} is missing");
      return false;
    }

    DirectoryCopier.DeleteContents(data.Path, ExportPolicy.IsPreservedOnImport, log);
    CopyResult result = DirectoryCopier.Copy(backup.Path, data.Path, null, log);

    if (result.FileCount == 0)
    {
      log.Error($"restore from {backup.Name} copied nothing");
      return false;
    }

    log.Info($"restored {result.FileCount} files from {backup.Name}");
    return true;
  }

  public void Prune(RhinoVersion version, TransferLog log)
  {
    IReadOnlyList<BackupFolder> backups = List(version);

    foreach (BackupFolder stale in backups.Skip(MaximumPerVersion))
    {
      DirectoryCopier.TryDeleteDirectory(stale.Path, log);
      log.Info($"removed old restore point {stale.Name}");
    }
  }

}
