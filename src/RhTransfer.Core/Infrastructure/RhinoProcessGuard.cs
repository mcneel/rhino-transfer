using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// Rhino writes its in-memory settings out when it closes, so importing underneath a running
/// Rhino silently loses the import. Rhino 9's own commands avoid this by setting
/// FileSettingsService.PreventSettingsWriteOut; from outside the process the only safe answer
/// is to refuse.
/// </summary>
public static class RhinoProcessGuard
{

  private static IReadOnlyList<string> ProcessNames { get; } = ["Rhino", "Rhinoceros"];

  public static IReadOnlyList<string> FindRunning()
  {
    List<string> running = [];

    foreach (string name in ProcessNames)
    {
      try
      {
        if (Process.GetProcessesByName(name).Length > 0) running.Add(name);
      }
      catch (InvalidOperationException)
      {
        // Process list unavailable; treat as not running rather than blocking the user.
      }
      catch (PlatformNotSupportedException)
      {
      }
    }

    return running;
  }

  public static bool IsRunning => FindRunning().Any();

}
