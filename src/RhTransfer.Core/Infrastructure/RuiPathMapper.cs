using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// Converts toolbar file references between "absolute on this machine" and "relative inside a
/// .rhs". This is the whole reason a settings folder cannot simply be zipped and copied.
///
/// Ported from Rhino 9's WindowLayoutBundle.GetRelativePath (export) and
/// TransferSettingsUtilities.TryGetAbsolutePath / ReplaceForeignPath (import), with the
/// RuiCache lookup replaced by classifying the path itself.
///
/// Separators matter here. A path that came off this machine is read with native semantics,
/// because Rhino has been known to bake a whole Windows path into a single macOS file name
/// (settings/Scheme__Default/imported_rui_files/"D:\Mijn GoogleDrive\...\x.rui" is one file,
/// not four folders). Only a path known to be foreign is split on both separators.
/// </summary>
public sealed class RuiPathMapper
{

  private const string ExternalFolder = "external";
  private const string ImportedFolder = "imported_rui_files";

  private RhinoDataFolder Data { get; }

  private string SettingsFolder { get; }

  public RuiPathMapper(RhinoDataFolder data)
  {
    Data = data;
    SettingsFolder = data.SettingsPath;
  }

  public RuiLocation ClassifyLocal(string localPath)
  {
    if (IsUnder(localPath, Data.UiPath)) return RuiLocation.DefaultUi;
    if (IsUnder(localPath, Data.PackagesPath)) return RuiLocation.Package;
    if (IsUnder(localPath, Data.PlugInsPath)) return RuiLocation.PlugIn;

    if (IsUnder(localPath, SettingsFolder) && HasImportedRuiParent(localPath)) return RuiLocation.ImportedRui;

    return RuiLocation.External;
  }

  /// <summary>
  /// Export direction. The result always uses forward slashes as separators, matching Rhino 9's
  /// archive entries.
  /// </summary>
  public string ToArchivePath(string localPath)
  {
    RuiLocation location = ClassifyLocal(localPath);

    return location switch
    {
      RuiLocation.DefaultUi => $"UI/{LocalFileName(localPath)}",
      RuiLocation.Package => TailFrom(localPath, "packages") ?? ExternalEntry(localPath),
      RuiLocation.PlugIn => TailFrom(localPath, "Plug-ins") ?? ExternalEntry(localPath),
      RuiLocation.ImportedRui => RelativeToSettings(localPath) ?? ExternalEntry(localPath),
      RuiLocation.External => ExternalEntry(localPath)
    };
  }

  /// <summary>Where an external toolbar file is staged, matching what <see cref="ToArchivePath"/> writes.</summary>
  public static string ExternalEntry(string localPath) => $"{ExternalFolder}/{LocalFileName(localPath)}";

  /// <summary>
  /// Where the file behind <see cref="ToArchivePath"/> physically sits in the archive. Imported
  /// toolbar references are written relative to the settings folder rather than the archive
  /// root, so the reference and the entry are not always the same string.
  /// </summary>
  public string ToArchiveEntry(string localPath)
  {
    string archivePath = ToArchivePath(localPath);

    return ClassifyArchive(archivePath) == RuiLocation.ImportedRui ? $"settings/{archivePath}" : archivePath;
  }

  public RuiLocation? ClassifyArchive(string archivePath)
  {
    string normalised = archivePath.Replace('\\', '/');

    if (normalised.StartsWith($"{ExternalFolder}/", StringComparison.OrdinalIgnoreCase)) return RuiLocation.External;
    if (normalised.StartsWith("UI/", StringComparison.OrdinalIgnoreCase)) return RuiLocation.DefaultUi;
    if (normalised.StartsWith("packages/", StringComparison.OrdinalIgnoreCase)) return RuiLocation.Package;
    if (normalised.StartsWith("Plug-ins/", StringComparison.OrdinalIgnoreCase)) return RuiLocation.PlugIn;

    if (normalised.StartsWith("Scheme__", StringComparison.OrdinalIgnoreCase)
        && normalised.Contains(ImportedFolder, StringComparison.OrdinalIgnoreCase))
      return RuiLocation.ImportedRui;

    return null;
  }

  /// <summary>
  /// Import direction. Returns the input unchanged when the reference cannot be resolved, so a
  /// layout pointing at something genuinely missing stays readable rather than being mangled.
  /// </summary>
  public string ToLocalPath(string archivePath)
  {
    RuiLocation? location = ClassifyArchive(archivePath);

    if (location is null) return ResolveForeign(archivePath);

    return location.Value switch
    {
      RuiLocation.External => Path.Combine(Data.Path, ExternalFolder, ArchiveTail(archivePath, ExternalFolder)),
      RuiLocation.DefaultUi => Path.Combine(Data.UiPath, ArchiveTail(archivePath, "UI")),
      RuiLocation.ImportedRui => Path.Combine(SettingsFolder, ToNativeArchivePath(archivePath)),
      RuiLocation.Package => FindByName(Path.Combine(Data.PackagesPath, Data.Version.FolderName), ArchiveFileName(archivePath)) ?? StagedExternal(archivePath) ?? archivePath,
      RuiLocation.PlugIn => FindByName(Data.PlugInsPath, ArchiveFileName(archivePath)) ?? StagedExternal(archivePath) ?? archivePath
    };
  }

  /// <summary>
  /// The copy the export staged under external/, for a package or plug-in toolbar with nothing
  /// installed here to match it.
  /// </summary>
  private string? StagedExternal(string archivePath)
  {
    string candidate = Path.Combine(Data.Path, ExternalFolder, ArchiveFileName(archivePath));

    return File.Exists(candidate) ? candidate : null;
  }

  /// <summary>
  /// An absolute path from another machine. If a file of the same name exists somewhere in this
  /// data folder, point at that; otherwise leave it be.
  /// </summary>
  private string ResolveForeign(string path)
  {
    if (File.Exists(path)) return path;
    if (!path.EndsWith(".rui", StringComparison.OrdinalIgnoreCase)) return path;

    return FindByName(Data.Path, ForeignFileName(path)) ?? path;
  }

  private static string? FindByName(string root, string fileName)
  {
    if (!Directory.Exists(root) || string.IsNullOrEmpty(fileName)) return null;

    try
    {
      return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return null;
    }
  }

  private string? RelativeToSettings(string localPath)
  {
    if (!IsUnder(localPath, SettingsFolder)) return null;

    string tail = localPath[SettingsFolder.Length..].TrimStart(Path.DirectorySeparatorChar);
    return ToArchiveSeparators(tail);
  }

  /// <summary>
  /// Everything from the named folder onwards, e.g. "packages/9.0/foo/1.2/foo.rui". The path is
  /// local, so only the native separator splits it.
  /// </summary>
  private static string? TailFrom(string path, string folderName)
  {
    List<string> segments = [.. path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)];

    int index = segments.FindLastIndex(s => string.Equals(s, folderName, StringComparison.OrdinalIgnoreCase));
    if (index < 0) return null;

    return string.Join('/', segments.Skip(index));
  }

  private bool HasImportedRuiParent(string localPath)
  {
    string? parent = Path.GetDirectoryName(localPath);

    return parent is not null
      && string.Equals(Path.GetFileName(parent), ImportedFolder, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>A path from this machine, so the native separator is the only separator.</summary>
  private static string LocalFileName(string path) => Path.GetFileName(path);

  /// <summary>A path from an unknown machine, so either separator could be the real one.</summary>
  private static string ForeignFileName(string path)
  {
    // The array is not decoration: Split('/', '\\', options) binds to Split(char, int, options)
    // because char converts implicitly to int, and quietly never splits on the backslash.
    string[] segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
    return segments.Length == 0 ? path : segments[^1];
  }

  /// <summary>Archive entries always use forward slashes, whatever the file names contain.</summary>
  private static string ArchiveFileName(string archivePath)
  {
    string[] segments = archivePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    return segments.Length == 0 ? archivePath : segments[^1];
  }

  private static string ArchiveTail(string archivePath, string prefix)
  {
    string normalised = archivePath.Replace('\\', '/');
    string tail = normalised[Math.Min(prefix.Length + 1, normalised.Length)..];
    return ToNativeArchivePath(tail);
  }

  private static string ToNativeArchivePath(string archivePath)
    => archivePath.Replace('/', Path.DirectorySeparatorChar);

  private static string ToArchiveSeparators(string localPath)
    => Path.DirectorySeparatorChar == '/' ? localPath : localPath.Replace(Path.DirectorySeparatorChar, '/');

  private static bool IsUnder(string path, string folder)
  {
    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;

    string trimmed = folder.TrimEnd(Path.DirectorySeparatorChar);

    return path.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
  }

}
