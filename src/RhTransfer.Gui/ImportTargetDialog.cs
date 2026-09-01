using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Eto.Drawing;
using Eto.Forms;

using RhTransfer.Core.Domain;

namespace RhTransfer.Gui;

/// <summary>
/// Asks which Rhino a settings file should go into. Importing is the one place where the
/// destination is a real choice: the file may have come from a different version, and the
/// target may be a Rhino that has never been started.
/// </summary>
public sealed class ImportTargetDialog : Dialog<RhinoInstall?>
{

  private IReadOnlyList<RhinoInstall> Installs { get; }

  private DropDown Targets { get; } = new();

  public ImportTargetDialog(string archivePath, IReadOnlyList<RhinoInstall> installs, RhsMetadata? metadata, RhinoInstall? preferred)
  {
    Installs = installs;

    Title = "Import Rhino settings";
    Resizable = false;
    Padding = 16;
    MinimumSize = new Size(420, 0);

    foreach (RhinoInstall install in installs)
    {
      Targets.Items.Add(install.ToString());
    }

    Targets.SelectedIndex = preferred is null
      ? installs.Count - 1
      : installs.Select((install, index) => (install, index)).First(pair => pair.install == preferred).index;

    Button import = new() { Text = "Import" };
    import.Click += (_, _) => Close(Installs.ElementAtOrDefault(Targets.SelectedIndex));

    Button cancel = new() { Text = "Cancel" };
    cancel.Click += (_, _) => Close(null);

    DefaultButton = import;
    AbortButton = cancel;

    Content = BuildLayout(archivePath, metadata, import, cancel);
  }

  private Control BuildLayout(string archivePath, RhsMetadata? metadata, Button import, Button cancel)
  {
    DynamicLayout layout = new() { Spacing = new Size(8, 10) };

    layout.AddRow(new Label
    {
      Text = Path.GetFileName(archivePath),
      Font = Fonts.Sans(13, FontStyle.Bold)
    });

    layout.AddRow(new Label
    {
      Text = Origin(metadata),
      TextColor = SystemColors.DisabledText,
      Wrap = WrapMode.Word
    });

    layout.AddRow(new Label { Text = "Import into" });
    layout.AddRow(Targets);

    layout.AddRow(new Label
    {
      Text = "The settings already there will be backed up first. Rhino must be closed.",
      TextColor = SystemColors.DisabledText,
      Wrap = WrapMode.Word
    });

    layout.AddRow(new TableLayout
    {
      Spacing = new Size(8, 0),
      Rows = { new TableRow(null, cancel, import) }
    });

    return layout;
  }

  private static string Origin(RhsMetadata? metadata) => metadata is null
    ? "This file does not say which Rhino it came from."
    : $"Exported from Rhino {metadata.SourceMajor} on {metadata.SourceOs}, {metadata.CreatedOn:d MMMM yyyy}.";

}
