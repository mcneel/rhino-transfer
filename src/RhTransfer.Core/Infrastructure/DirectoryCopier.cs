using System;
using System.Collections.Generic;
using System.IO;

using RhTransfer.Core.Application;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// Recursive copy and delete that keeps going after a single file fails, reporting what it
/// could not do rather than aborting halfway. Ported from Rhino 9's CloneDirectories/ForceCopy.
/// </summary>
public static class DirectoryCopier
{

  /// <summary>
  /// Copies <paramref name="source"/> into <paramref name="destination"/>. The predicate sees
  /// each entry's path relative to <paramref name="source"/> with forward slashes, and returns
  /// whether to take it. A null predicate takes everything.
  /// </summary>
  public static CopyResult Copy(string source, string destination, Func<string, bool>? include, TransferLog log)
  {
    if (!Directory.Exists(source)) return default;

    CopyResult total = default;
    Directory.CreateDirectory(destination);

    foreach (string file in EnumerateFiles(source, log))
    {
      string relative = RelativePath(source, file);
      if (include is not null && !include(relative)) continue;

      string target = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));

      if (!TryCopyFile(file, target, log)) continue;

      total += new CopyResult(1, SizeOf(file));
    }

    return total;
  }

  public static bool TryCopyFile(string source, string destination, TransferLog log)
  {
    try
    {
      string? folder = Path.GetDirectoryName(destination);
      if (folder is not null) Directory.CreateDirectory(folder);

      ClearReadOnly(destination);
      File.Copy(source, destination, true);
      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      log.Warn($"could not copy {source}: {exception.Message}");
      return false;
    }
  }

  /// <summary>
  /// Empties a folder. Entries whose top-level name satisfies <paramref name="preserve"/> are
  /// left in place, which is how licensing survives an import.
  /// </summary>
  public static void DeleteContents(string folder, Func<string, bool> preserve, TransferLog log)
  {
    if (!Directory.Exists(folder)) return;

    foreach (string directory in Directory.EnumerateDirectories(folder))
    {
      if (preserve(new DirectoryInfo(directory).Name)) continue;
      TryDeleteDirectory(directory, log);
    }

    foreach (string file in Directory.EnumerateFiles(folder))
    {
      if (preserve(Path.GetFileName(file))) continue;
      TryDeleteFile(file, log);
    }
  }

  public static void TryDeleteDirectory(string directory, TransferLog log)
  {
    if (!Directory.Exists(directory)) return;

    try
    {
      Directory.Delete(directory, true);
      return;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Fall through and try one file at a time so one locked file does not strand the rest.
    }

    foreach (string child in Directory.EnumerateDirectories(directory))
    {
      TryDeleteDirectory(child, log);
    }

    foreach (string file in Directory.EnumerateFiles(directory))
    {
      TryDeleteFile(file, log);
    }

    try
    {
      Directory.Delete(directory, true);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      log.Warn($"could not remove {directory}: {exception.Message}");
    }
  }

  private static void TryDeleteFile(string file, TransferLog log)
  {
    try
    {
      ClearReadOnly(file);
      File.Delete(file);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      log.Warn($"could not remove {file}: {exception.Message}");
    }
  }

  public static IEnumerable<string> EnumerateFiles(string root, TransferLog log)
  {
    try
    {
      return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      log.Warn($"could not read {root}: {exception.Message}");
      return [];
    }
  }

  /// <summary>
  /// Archive-style relative path. Only the native separator is translated: on macOS a backslash
  /// is a legal character in a file name and Rhino does put whole Windows paths into one.
  /// </summary>
  public static string RelativePath(string root, string path)
  {
    string relative = Path.GetRelativePath(root, path);
    return Path.DirectorySeparatorChar == '/' ? relative : relative.Replace(Path.DirectorySeparatorChar, '/');
  }

  public static long SizeOf(string file)
  {
    try
    {
      return new FileInfo(file).Length;
    }
    catch (IOException)
    {
      return 0;
    }
  }

  private static void ClearReadOnly(string path)
  {
    if (!File.Exists(path)) return;

    FileAttributes attributes = File.GetAttributes(path);
    if ((attributes & FileAttributes.ReadOnly) == 0) return;

    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
  }

}
