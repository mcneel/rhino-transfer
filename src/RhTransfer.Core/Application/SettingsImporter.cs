using System;
using System.Collections.Generic;
using System.IO;

using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

namespace RhTransfer.Core.Application;

/// <summary>
/// Replaces a Rhino data folder with the contents of a .rhs, taking a restore point first.
/// </summary>
public sealed class SettingsImporter
{

  private BackupStore Backups { get; }

  /// <summary>
  /// Which Rhino processes are running. Injected because it is ambient machine state: a test
  /// must not pass or fail depending on whether the developer happens to have Rhino open.
  /// </summary>
  private Func<IReadOnlyList<string>> FindRunningRhino { get; }

  public SettingsImporter(BackupStore backups, Func<IReadOnlyList<string>>? findRunningRhino = null)
  {
    Backups = backups;
    FindRunningRhino = findRunningRhino ?? RhinoProcessGuard.FindRunning;
  }

  public TransferReport Import(string archivePath, RhinoDataFolder target, bool isDryRun = false)
  {
    TransferLog log = new();

    if (!File.Exists(archivePath))
      return log.ToFailure($"{archivePath} does not exist");

    if (!RhsArchive.IsReadable(archivePath, out string reason))
      return log.ToFailure($"{archivePath} is not a readable settings file: {reason}");

    if (!TransferLimits.CanImportInto(target.Version))
      return log.ToFailure($"settings cannot be written into Rhino {target.Version.Major}. A settings file uses the window layout format introduced in Rhino {TransferLimits.EarliestImportTarget.Major}.");

    ReportProvenance(archivePath, target, log);

    if (isDryRun)
    {
      log.Info($"dry run: would replace {target.Path}");
      return log.ToReport(0, 0);
    }

    IReadOnlyList<string> running = FindRunningRhino();
    if (running.Count > 0)
      return log.ToFailure($"{string.Join(" and ", running)} is running. Close Rhino and try again, or it will overwrite these settings when it quits.");

    Directory.CreateDirectory(target.Path);

    BackupOutcome outcome = Backups.Create(target, log);

    if (outcome is BackupOutcome.Failed failure)
    {
      log.Error(failure.Reason);
      log.Error("nothing has been changed");
      return log.ToReport(0, 0);
    }

    BackupFolder? backup = (outcome as BackupOutcome.Created)?.Folder;

    try
    {
      DirectoryCopier.DeleteContents(target.Path, ExportPolicy.IsPreservedOnImport, log);

      CopyResult extracted = RhsArchive.Extract(archivePath, target.Path, log);

      if (extracted.FileCount == 0)
      {
        log.Error("the settings file contained nothing that could be extracted");
        return RollBack(backup, target, log);
      }

      RestoreLayoutPaths(target, log);
      Backups.Prune(target.Version, log);

      log.Info($"restored {extracted.FileCount} files ({Megabytes(extracted.ByteCount)}) into {target.Path}");
      log.Info("start Rhino to pick up the new settings");

      return log.ToReport(extracted.FileCount, extracted.ByteCount, backup?.Path);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
    {
      log.Error(exception.Message);
      return RollBack(backup, target, log);
    }
  }

  public TransferReport Restore(BackupFolder backup, RhinoDataFolder target)
  {
    TransferLog log = new();

    IReadOnlyList<string> running = FindRunningRhino();
    if (running.Count > 0)
      return log.ToFailure($"{string.Join(" and ", running)} is running. Close Rhino and try again.");

    if (!Backups.Restore(backup, target, log)) return log.ToReport(0, 0);

    RestoreLayoutPaths(target, log);
    return log.ToReport(0, 0, backup.Path);
  }

  private TransferReport RollBack(BackupFolder? backup, RhinoDataFolder target, TransferLog log)
  {
    if (backup is null)
    {
      log.Info("clearing the half written settings folder, there was nothing here before");
      DirectoryCopier.DeleteContents(target.Path, ExportPolicy.IsPreservedOnImport, log);
      return log.ToReport(0, 0);
    }

    log.Info($"rolling back to {backup.Name}");

    if (!Backups.Restore(backup, target, log))
      log.Error($"roll back failed. Your previous settings are still in {backup.Path}");

    return log.ToReport(0, 0, backup.Path);
  }

  /// <summary>
  /// Point every toolbar reference at somewhere on this machine. Without this the window layout
  /// still names the other machine's folders and the toolbars come up empty.
  /// </summary>
  private static void RestoreLayoutPaths(RhinoDataFolder target, TransferLog log)
  {
    RuiPathMapper mapper = new(target);
    int rewritten = 0;
    HashSet<string> unresolved = new(StringComparer.OrdinalIgnoreCase);

    foreach (string layout in WindowLayoutFile.Find(target.SettingsPath))
    {
      rewritten += WindowLayoutFile.RewriteReferences(layout, mapper.ToLocalPath);

      foreach (string reference in WindowLayoutFile.ReadReferences(layout))
      {
        if (!File.Exists(reference)) unresolved.Add(reference);
      }
    }

    if (rewritten > 0) log.Info($"relinked {rewritten} toolbar references to this machine");

    ReportUnresolved(unresolved, log);
  }

  /// <summary>
  /// A layout can name a toolbar this machine does not have and the archive did not carry, which
  /// is what a Rhino 9 export leaves behind for a package toolbar. Say so rather than leaving the
  /// user to wonder why a toolbar is empty.
  /// </summary>
  private static void ReportUnresolved(IReadOnlyCollection<string> unresolved, TransferLog log)
  {
    if (unresolved.Count == 0) return;

    foreach (string reference in unresolved)
    {
      string name = Path.GetFileName(reference.Replace('\\', '/'));

      bool isPackage = reference.Replace('\\', '/').Contains("/packages/", StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith("packages/", StringComparison.OrdinalIgnoreCase);

      log.Warn(isPackage
        ? $"toolbar {name} belongs to a plug-in package that is not installed here. Install it and the toolbar will come back."
        : $"toolbar {name} could not be found on this machine, so that toolbar will be empty.");
    }
  }

  private static void ReportProvenance(string archivePath, RhinoDataFolder target, TransferLog log)
  {
    RhsMetadata? metadata = RhsArchive.ReadMetadata(archivePath);

    if (metadata is null)
    {
      log.Info("settings file has no provenance, treating it as a Rhino export");
      return;
    }

    log.Info($"settings file from Rhino {metadata.SourceMajor} on {metadata.SourceOs}, made {metadata.CreatedOn:yyyy-MM-dd}");

    if (metadata.SourceMajor != target.Version.Major)
    {
      log.Warn($"this came from Rhino {metadata.SourceMajor} and you are importing into Rhino {target.Version.Major}. Window layouts and toolbars may not survive the version change.");
    }

    if (!string.Equals(metadata.SourceOs, RhsArchive.OsName, StringComparison.OrdinalIgnoreCase))
    {
      log.Warn($"this came from {metadata.SourceOs} and you are on {RhsArchive.OsName}. Anything platform specific will be ignored by Rhino.");
    }
  }

  private static string Megabytes(long bytes) => $"{bytes / 1024d / 1024d:0.0} MB";

}
