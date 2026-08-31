using System;
using System.Collections.Generic;
using System.IO;

using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

namespace RhTransfer.Core.Application;

/// <summary>
/// Reads a Rhino data folder and writes a .rhs. Rhino does not need to be installed, let alone
/// running: everything comes off disk.
/// </summary>
public sealed class SettingsExporter
{

  private string ToolVersion { get; }

  public SettingsExporter(string toolVersion)
  {
    ToolVersion = toolVersion;
  }

  public TransferReport Export(RhinoDataFolder source, string outputPath, bool isDryRun = false)
  {
    TransferLog log = new();

    if (!Directory.Exists(source.Path))
      return log.ToFailure($"{source.Path} does not exist");

    string staging = Path.Combine(Path.GetTempPath(), "rh-transfer", Guid.NewGuid().ToString("n"));

    try
    {
      CopyResult copied = DirectoryCopier.Copy(source.Path, staging, ExportPolicy.ShouldExport, log);

      if (copied.FileCount == 0)
        return log.ToFailure($"nothing to export from {source.Path}");

      CopyResult external = CollectExternalToolbars(source, staging, log);
      RewriteLayouts(source, staging, log);
      Scrub(staging, log);

      CopyResult staged = copied + external;

      if (isDryRun)
      {
        log.Info($"dry run: {staged.FileCount} files ({Megabytes(staged.ByteCount)}) would be written to {outputPath}");
        return log.ToReport(staged.FileCount, staged.ByteCount);
      }

      RhsMetadata metadata = new(
        RhsMetadata.ToolName,
        ToolVersion,
        source.Version.Major,
        RhsArchive.OsName,
        DateTimeOffset.Now);

      CopyResult written = RhsArchive.Write(staging, outputPath, metadata, log);

      if (written.FileCount == 0)
        return log.ToFailure($"could not write {outputPath}");

      log.Info($"wrote {written.FileCount} files ({Megabytes(written.ByteCount)}) to {outputPath}");
      return log.ToReport(written.FileCount, written.ByteCount);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return log.ToFailure(exception.Message);
    }
    finally
    {
      DirectoryCopier.TryDeleteDirectory(staging, log);
    }
  }

  /// <summary>
  /// Toolbar files a layout points at that live outside the data folder, for example on the
  /// user's Desktop. They go under external/ so the other machine has something to resolve.
  /// Package toolbars are deliberately left out: those come back with the package itself.
  /// </summary>
  private static CopyResult CollectExternalToolbars(RhinoDataFolder source, string staging, TransferLog log)
  {
    RuiPathMapper mapper = new(source);
    HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    CopyResult total = default;

    foreach (string layout in WindowLayoutFile.Find(Path.Combine(staging, "settings")))
    {
      foreach (string reference in WindowLayoutFile.ReadReferences(layout))
      {
        if (mapper.ClassifyLocal(reference) != RuiLocation.External) continue;
        if (!seen.Add(reference)) continue;

        if (!File.Exists(reference))
        {
          log.Warn($"toolbar file is missing, layout reference left as is: {reference}");
          continue;
        }

        string target = Path.Combine(staging, "external", Path.GetFileName(reference));
        if (!DirectoryCopier.TryCopyFile(reference, target, log)) continue;

        total += new CopyResult(1, DirectoryCopier.SizeOf(reference));
      }
    }

    if (total.FileCount > 0) log.Info($"included {total.FileCount} custom toolbar files");

    return total;
  }

  private static void RewriteLayouts(RhinoDataFolder source, string staging, TransferLog log)
  {
    RuiPathMapper mapper = new(source);
    int rewritten = 0;

    foreach (string layout in WindowLayoutFile.Find(Path.Combine(staging, "settings")))
    {
      rewritten += WindowLayoutFile.RewriteReferences(layout, mapper.ToArchivePath);
    }

    if (rewritten > 0) log.Info($"made {rewritten} toolbar references portable");
  }

  private static void Scrub(string staging, TransferLog log)
  {
    string settings = Path.Combine(staging, "settings");
    if (!Directory.Exists(settings)) return;

    int removed = 0;

    foreach (string file in Directory.EnumerateFiles(settings, "settings-Scheme__*.xml", SearchOption.TopDirectoryOnly))
    {
      int? count = IdentityScrubber.Scrub(file);

      if (count is null)
      {
        log.Warn($"could not read {Path.GetFileName(file)} to remove personal data");
        continue;
      }

      removed += count.Value;
    }

    log.Info(removed == 0
      ? "no personal data found to remove"
      : $"removed {removed} personal entries (name, address, email, licence refresh, recent files, telemetry)");
  }

  private static string Megabytes(long bytes) => $"{bytes / 1024d / 1024d:0.0} MB";

}
