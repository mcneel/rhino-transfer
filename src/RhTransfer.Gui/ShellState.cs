using System.Collections.Generic;
using System.Collections.Immutable;

using RhTransfer.Core.Domain;

namespace RhTransfer.Gui;

/// <summary>
/// Everything the window shows. The UI is a function of this: actions produce a new state and
/// the form redraws from it, so there is no way for a control and the model to disagree.
/// </summary>
public sealed record ShellState
{

  public required IReadOnlyList<RhinoDataFolder> Folders { get; init; }

  public required int SelectedIndex { get; init; }

  public required ImmutableArray<TransferMessage> Messages { get; init; }

  public required string Status { get; init; }

  public bool IsBusy { get; init; }

  public RhinoDataFolder? Selected
    => SelectedIndex >= 0 && SelectedIndex < Folders.Count ? Folders[SelectedIndex] : null;

  public bool CanTransfer => !IsBusy && Selected is not null;

  public static ShellState Initial(IReadOnlyList<RhinoDataFolder> folders) => new()
  {
    Folders = folders,
    SelectedIndex = folders.Count > 0 ? folders.Count - 1 : -1,
    Messages = [],
    Status = folders.Count == 0
      ? "No Rhino settings found on this machine."
      : "Choose a Rhino version, then export or import."
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
