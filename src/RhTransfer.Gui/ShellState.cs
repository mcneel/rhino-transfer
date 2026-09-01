using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using RhTransfer.Core.Domain;

namespace RhTransfer.Gui;

/// <summary>
/// Everything the window shows. The UI is a function of this: actions produce a new state and
/// the form redraws from it, so there is no way for a control and the model to disagree.
///
/// The dropdown lists what can be exported. Import can go anywhere Rhino is installed, including
/// a version with no settings yet, so it asks for its destination separately.
/// </summary>
public sealed record ShellState
{

  public required IReadOnlyList<RhinoInstall> Installs { get; init; }

  public required int SelectedIndex { get; init; }

  public required ImmutableArray<TransferMessage> Messages { get; init; }

  public required string Status { get; init; }

  public bool IsBusy { get; init; }

  public IReadOnlyList<RhinoInstall> Exportable => [.. Installs.Where(i => i.CanExport)];

  public IReadOnlyList<RhinoInstall> ImportTargets => [.. Installs.Where(i => i.CanImport)];

  public RhinoInstall? Selected
    => SelectedIndex >= 0 && SelectedIndex < Exportable.Count ? Exportable[SelectedIndex] : null;

  public bool CanExport => !IsBusy && Selected is not null;

  public bool CanImport => !IsBusy && ImportTargets.Count > 0;

  public static ShellState Initial(IReadOnlyList<RhinoInstall> installs)
  {
    int exportable = installs.Count(i => i.CanExport);

    return new()
    {
      Installs = installs,
      SelectedIndex = exportable > 0 ? exportable - 1 : -1,
      Messages = [],
      Status = Describe(installs, exportable)
    };
  }

  private static string Describe(IReadOnlyList<RhinoInstall> installs, int exportable) => installs.Count switch
  {
    0 => "No Rhino found on this machine.",
    _ when exportable == 0 => "Rhino is installed but has no settings yet. You can import into it.",
    _ when installs.All(i => !i.CanImport) => "The Rhino found here cannot have settings written into it.",
    _ => "Export the settings of the chosen Rhino, or import a settings file into any of them."
  };

  public ShellState Working(string status) => this with { IsBusy = true, Status = status, Messages = [] };

  public ShellState Finished(TransferReport report, string success, string failure) => this with
  {
    IsBusy = false,
    Messages = report.Messages,
    Status = report.IsSuccess ? success : failure
  };

  public ShellState Failed(string status) => this with { IsBusy = false, Status = status, Messages = [] };

}
