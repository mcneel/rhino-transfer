using System;
using System.Collections.Generic;
using System.Globalization;

namespace RhTransfer.Cli;

public static class CommandLineParser
{

  public static bool TryParse(IReadOnlyList<string> args, out CommandLineOptions options, out string error)
  {
    options = new CommandLineOptions { Verb = Verb.Help };
    error = string.Empty;

    if (args.Count == 0) return true;

    if (!TryParseVerb(args[0], out Verb verb))
    {
      error = $"unknown command '{args[0]}'";
      return false;
    }

    int? major = null;
    string? file = null;
    string? backup = null;
    string? dataRoot = null;
    bool isDryRun = false;

    for (int i = 1; i < args.Count; i++)
    {
      string argument = args[i];

      switch (argument)
      {
        case "--dry-run":
          isDryRun = true;
          continue;

        case "-h":
        case "--help":
          options = new CommandLineOptions { Verb = Verb.Help };
          return true;
      }

      if (!TryTakeValue(args, ref i, argument, out string value, out error)) return false;

      switch (argument)
      {
        case "--version":
        case "-v":
          if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
          {
            error = $"'{value}' is not a Rhino version number";
            return false;
          }
          major = parsed;
          break;

        case "--out":
        case "--in":
        case "--file":
          file = value;
          break;

        case "--backup":
          backup = value;
          break;

        case "--data-root":
          dataRoot = value;
          break;

        default:
          error = $"unknown option '{argument}'";
          return false;
      }
    }

    options = new CommandLineOptions
    {
      Verb = verb,
      Major = major,
      File = file,
      Backup = backup,
      DataRoot = dataRoot,
      IsDryRun = isDryRun
    };

    return true;
  }

  private static bool TryParseVerb(string value, out Verb verb)
  {
    switch (value)
    {
      case "list": verb = Verb.List; return true;
      case "export": verb = Verb.Export; return true;
      case "import": verb = Verb.Import; return true;
      case "restore": verb = Verb.Restore; return true;
      case "help":
      case "-h":
      case "--help": verb = Verb.Help; return true;
      default: verb = Verb.Help; return false;
    }
  }

  private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, string argument, out string value, out string error)
  {
    if (index + 1 >= args.Count)
    {
      value = string.Empty;
      error = $"'{argument}' needs a value";
      return false;
    }

    index++;
    value = args[index];
    error = string.Empty;
    return true;
  }

}
