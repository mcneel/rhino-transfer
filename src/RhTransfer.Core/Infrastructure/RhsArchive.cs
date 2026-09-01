using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// A .rhs file: a plain zip with a renamed extension, laid out exactly as Rhino 9 lays it out,
/// so archives move both ways between this tool and OptionsExport/OptionsMigrate.
/// </summary>
public static class RhsArchive
{

  public const string Extension = ".rhs";

  public static string DefaultFileName(RhinoVersion version) => $"Settings_{version.Major}_{OsName}{Extension}";

  public static string OsName
    => OperatingSystem.IsWindows() ? "win"
     : OperatingSystem.IsMacOS() ? "mac"
     : OperatingSystem.IsLinux() ? "linux"
     : "unknown";

  /// <summary>
  /// Zips everything under <paramref name="stagingFolder"/>. Entry names always use forward
  /// slashes: a backslash in an entry name makes macOS create files with slashes in the name.
  /// </summary>
  public static CopyResult Write(string stagingFolder, string outputPath, RhsMetadata metadata, TransferLog log)
  {
    if (File.Exists(outputPath)) File.Delete(outputPath);

    string? folder = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

    CopyResult total = default;

    using (ZipArchive archive = ZipFile.Open(outputPath, ZipArchiveMode.Create))
    {
      archive.Comment = JsonSerializer.Serialize(metadata, RhsMetadataContext.Default.RhsMetadata);

      foreach (string file in DirectoryCopier.EnumerateFiles(stagingFolder, log))
      {
        string entryName = DirectoryCopier.RelativePath(stagingFolder, file);

        try
        {
          archive.CreateEntryFromFile(file, entryName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
          log.Warn($"could not add {entryName}: {exception.Message}");
          continue;
        }

        total += new CopyResult(1, DirectoryCopier.SizeOf(file));
      }
    }

    return total;
  }

  public static CopyResult Extract(string archivePath, string destination, TransferLog log)
  {
    CopyResult total = default;
    Directory.CreateDirectory(destination);

    string root = Path.GetFullPath(destination);

    using ZipArchive archive = ZipFile.OpenRead(archivePath);

    foreach (ZipArchiveEntry entry in archive.Entries)
    {
      if (entry.FullName.EndsWith('/')) continue;

      string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

      if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
      {
        log.Warn($"skipped {entry.FullName}: entry points outside the target folder");
        continue;
      }

      try
      {
        string? folder = Path.GetDirectoryName(target);
        if (folder is not null) Directory.CreateDirectory(folder);

        entry.ExtractToFile(target, true);
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
      {
        log.Warn($"could not extract {entry.FullName}: {exception.Message}");
        continue;
      }

      total += new CopyResult(1, entry.Length);
    }

    return total;
  }

  public static RhsMetadata? ReadMetadata(string archivePath)
  {
    try
    {
      using ZipArchive archive = ZipFile.OpenRead(archivePath);
      if (string.IsNullOrWhiteSpace(archive.Comment)) return null;

      return JsonSerializer.Deserialize(archive.Comment, RhsMetadataContext.Default.RhsMetadata);
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException)
    {
      return null;
    }
  }

  public static bool IsReadable(string archivePath, out string reason)
  {
    try
    {
      using ZipArchive archive = ZipFile.OpenRead(archivePath);
      reason = string.Empty;
      return archive.Entries.Count > 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException)
    {
      reason = exception.Message;
      return false;
    }
  }

}
