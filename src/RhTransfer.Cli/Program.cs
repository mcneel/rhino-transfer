using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

namespace RhTransfer.Cli;

public static class Program
{

  private const int ExitSuccess = 0;
  private const int ExitFailure = 1;
  private const int ExitUsage = 2;

  public static int Main(string[] args)
  {
    if (!CommandLineParser.TryParse(args, out CommandLineOptions options, out string error))
    {
      Console.Error.WriteLine($"rh-transfer: {error}");
      Console.Error.WriteLine();
      WriteUsage(Console.Error);
      return ExitUsage;
    }

    RhinoRoot root = options.DataRoot is null ? RhinoRoot.ForCurrentUser() : new RhinoRoot(options.DataRoot);

    return options.Verb switch
    {
      Verb.Help => Help(),
      Verb.List => List(root),
      Verb.Export => Export(root, options),
      Verb.Import => Import(root, options),
      Verb.Restore => Restore(root, options)
    };
  }

  private static int Help()
  {
    WriteUsage(Console.Out);
    return ExitSuccess;
  }

  private static int List(RhinoRoot root)
  {
    Console.WriteLine($"Rhino data folder: {root.Path}");
    Console.WriteLine();

    if (new RhinoInventory(root).All().Count == 0)
    {
      Console.WriteLine("No Rhino found.");
      return ExitSuccess;
    }

    Console.WriteLine("Rhino versions:");
    foreach (RhinoInstall install in new RhinoInventory(root).All())
    {
      Console.WriteLine($"  {install,-38}  {install.DataFolder.Path}");
    }

    IReadOnlyList<BackupFolder> backups = root.FindBackups();
    if (backups.Count == 0) return ExitSuccess;

    Console.WriteLine();
    Console.WriteLine("Restore points:");
    foreach (BackupFolder backup in backups)
    {
      Console.WriteLine($"  {backup.Name}");
    }

    return ExitSuccess;
  }

  private static int Export(RhinoRoot root, CommandLineOptions options)
  {
    if (!TryResolveVersion(root, options.Major, out RhinoDataFolder source, out string problem))
      return Fail(problem);

    string output = options.File ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
      RhsArchive.DefaultFileName(source.Version));

    Console.WriteLine($"Exporting Rhino {source.Version.Major} from {source.Path}");

    SettingsExporter exporter = new(ToolVersion);
    return Report(exporter.Export(source, output, options.IsDryRun));
  }

  private static int Import(RhinoRoot root, CommandLineOptions options)
  {
    if (options.File is null) return Fail("import needs --in <file.rhs>");

    if (!TryResolveTarget(root, options.Major, out RhinoDataFolder target, out string problem))
      return Fail(problem);

    Console.WriteLine($"Importing {options.File} into Rhino {target.Version.Major} at {target.Path}");

    SettingsImporter importer = new(new BackupStore(root));
    return Report(importer.Import(options.File, target, options.IsDryRun));
  }

  private static int Restore(RhinoRoot root, CommandLineOptions options)
  {
    if (!TryResolveVersion(root, options.Major, out RhinoDataFolder target, out string problem))
      return Fail(problem);

    IReadOnlyList<BackupFolder> backups = root.FindBackups(target.Version);

    if (backups.Count == 0)
      return Fail($"no restore points for Rhino {target.Version.Major}");

    BackupFolder? chosen = options.Backup is null
      ? backups[0]
      : backups.FirstOrDefault(b => string.Equals(b.Name, options.Backup, StringComparison.OrdinalIgnoreCase));

    if (chosen is null)
      return Fail($"no restore point named '{options.Backup}'");

    Console.WriteLine($"Restoring {chosen.Name} into {target.Path}");

    SettingsImporter importer = new(new BackupStore(root));
    return Report(importer.Restore(chosen, target));
  }

  /// <summary>
  /// An import target need not exist yet: a fresh machine has no data folder until Rhino runs
  /// once. An explicit --version is enough to name where it should go.
  /// </summary>
  private static bool TryResolveTarget(RhinoRoot root, int? major, out RhinoDataFolder folder, out string problem)
  {
    folder = null!;
    problem = string.Empty;

    if (major is null) return TryResolveVersion(root, null, out folder, out problem);

    folder = root.DataFolderFor(new RhinoVersion(major.Value));
    return true;
  }

  private static bool TryResolveVersion(RhinoRoot root, int? major, out RhinoDataFolder folder, out string problem)
  {
    IReadOnlyList<RhinoDataFolder> folders = root.FindDataFolders();
    folder = null!;
    problem = string.Empty;

    if (folders.Count == 0)
    {
      problem = $"no Rhino settings found under {root.Path}";
      return false;
    }

    if (major is null)
    {
      if (folders.Count > 1)
      {
        string available = string.Join(", ", folders.Select(f => f.Version.Major));
        problem = $"several Rhino versions are installed ({available}), pick one with --version";
        return false;
      }

      folder = folders[0];
      return true;
    }

    RhinoDataFolder? match = folders.FirstOrDefault(f => f.Version.Major == major.Value);

    if (match is null)
    {
      problem = $"no settings found for Rhino {major.Value} under {root.Path}";
      return false;
    }

    folder = match;
    return true;
  }

  private static int Report(TransferReport report)
  {
    foreach (TransferMessage message in report.Messages)
    {
      TextWriter destination = message.Severity == TransferSeverity.Error ? Console.Error : Console.Out;
      destination.WriteLine($"  {message}");
    }

    Console.WriteLine();
    Console.WriteLine(report.IsSuccess ? "Done." : "Failed.");

    return report.IsSuccess ? ExitSuccess : ExitFailure;
  }

  private static int Fail(string message)
  {
    Console.Error.WriteLine($"rh-transfer: {message}");
    return ExitFailure;
  }

  private static string ToolVersion
    => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

  private static void WriteUsage(TextWriter writer)
  {
    writer.WriteLine("""
      rh-transfer - move Rhino settings between machines and versions

      Usage:
        rh-transfer list
        rh-transfer export  [--version <major>] [--out <file.rhs>] [--dry-run]
        rh-transfer import   --in <file.rhs> [--version <major>] [--dry-run]
        rh-transfer restore [--version <major>] [--backup <name>]

      Options:
        --version, -v <major>   Which Rhino to read or write, e.g. 8. Optional when only one is installed.
        --out <file>            Where to write the settings file. Defaults to the Desktop.
        --in <file>             The settings file to import.
        --backup <name>         Which restore point to roll back to. Defaults to the newest.
        --data-root <path>      Use a different McNeel/Rhinoceros folder. Mostly for testing.
        --dry-run               Say what would happen without changing anything.

      Close Rhino before importing, or it will write its own settings back over yours when it quits.
      Personal details (name, address, email, licence refresh, recent files) are always removed on export.
      """);
  }

}
