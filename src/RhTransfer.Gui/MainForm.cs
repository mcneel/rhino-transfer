using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Eto.Drawing;
using Eto.Forms;

using RhTransfer.Core.Application;
using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

namespace RhTransfer.Gui;

/// <summary>
/// The whole interface: pick a Rhino version, then export settings to a file or import a file
/// over them.
/// </summary>
public sealed class MainForm : Form
{

  private RhinoRoot Root { get; }

  private RhinoInventory Inventory { get; }

  private ShellState State { get; set; }

  private DropDown Versions { get; } = new();
  private Button ExportButton { get; } = new() { Text = "Export Settings..." };
  private Button ImportButton { get; } = new() { Text = "Import Settings..." };
  private Label StatusLabel { get; } = new() { Wrap = WrapMode.Word };
  private TextArea Log { get; } = new() { ReadOnly = true, Wrap = false, Font = Fonts.Monospace(11) };

  public MainForm(RhinoRoot root)
  {
    Root = root;
    Inventory = new RhinoInventory(root);
    State = ShellState.Initial(Inventory.All());

    Title = "Rhino Settings Transfer";
    MinimumSize = new Size(560, 420);
    ClientSize = new Size(620, 460);

    Versions.SelectedIndexChanged += (_, _) =>
    {
      if (Versions.SelectedIndex == State.SelectedIndex) return;
      State = State with { SelectedIndex = Versions.SelectedIndex };
    };

    ExportButton.Click += async (_, _) => await ExportAsync().ConfigureAwait(true);
    ImportButton.Click += async (_, _) => await ImportAsync().ConfigureAwait(true);

    Content = BuildLayout();
    Render();
  }

  private Control BuildLayout()
  {
    Label heading = new()
    {
      Text = "Move your Rhino settings between machines",
      Font = Fonts.Sans(15, FontStyle.Bold)
    };

    Label note = new()
    {
      Text = "Close Rhino before importing. Personal details are always removed from an export.",
      Wrap = WrapMode.Word,
      TextColor = SystemColors.DisabledText
    };

    DynamicLayout layout = new() { Padding = new Padding(16), Spacing = new Size(8, 10) };

    layout.AddRow(heading);
    layout.AddRow(note);

    layout.AddRow(new TableLayout
    {
      Spacing = new Size(8, 8),
      Rows = {
        new TableRow(new Label { Text = "Rhino version" }),
        new TableRow(Versions),
        new TableRow(new (ExportButton, true), new (ImportButton, true))
        }
    });

    layout.AddRow(StatusLabel);
    layout.Add(Log, yscale: true);

    return layout;
  }

  /// <summary>
  /// Push the current state into the controls. The only place that touches them.
  /// </summary>
  private void Render()
  {
    if (Versions.Items.Count != State.Exportable.Count)
    {
      Versions.Items.Clear();

      foreach (RhinoInstall install in State.Exportable)
      {
        Versions.Items.Add(install.ToString());
      }
    }

    Versions.SelectedIndex = State.SelectedIndex;
    Versions.Enabled = !State.IsBusy && State.Exportable.Count > 0;

    ExportButton.Enabled = State.CanExport;
    ImportButton.Enabled = State.CanImport;

    StatusLabel.Text = State.Status;
    Log.Text = string.Join(Environment.NewLine, State.Messages.Select(m => m.ToString()));
  }

  private void Transition(ShellState next)
  {
    State = next;
    Render();
  }

  private async Task ExportAsync()
  {
    if (State.Selected is not RhinoInstall selected) return;

    RhinoDataFolder source = selected.DataFolder;

    using SaveFileDialog dialog = new()
    {
      Title = "Export Rhino settings",
      FileName = RhsArchive.DefaultFileName(source.Version),
      Directory = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
    };

    dialog.Filters.Add(new FileFilter("Rhino settings", ".rhs"));

    if (dialog.ShowDialog(this) != DialogResult.Ok) return;

    string target = EnsureExtension(dialog.FileName);

    Transition(State.Working($"Exporting Rhino {source.Version.Major}..."));

    SettingsExporter exporter = new(ToolVersion);
    TransferReport report = await Task.Run(() => exporter.Export(source, target)).ConfigureAwait(true);

    Transition(State.Finished(report, $"Exported to {target}", "Export failed."));
  }

  private async Task ImportAsync()
  {
    using OpenFileDialog dialog = new() { Title = "Import Rhino settings" };
    dialog.Filters.Add(new FileFilter("Rhino settings", ".rhs"));

    if (dialog.ShowDialog(this) != DialogResult.Ok) return;

    string archive = dialog.FileName;

    RhsMetadata? metadata = RhsArchive.ReadMetadata(archive);
    ImportTargetDialog chooser = new(archive, State.ImportTargets, metadata, Inventory.PreferredTarget(metadata));
    if (chooser.ShowModal(this) is not RhinoInstall destination) return;

    RhinoDataFolder target = destination.DataFolder;

    Transition(State.Working($"Importing into Rhino {target.Version.Major}..."));

    SettingsImporter importer = new(new BackupStore(Root));
    TransferReport report = await Task.Run(() => importer.Import(archive, target)).ConfigureAwait(true);

    string success = report.BackupPath is null
      ? "Imported. Start Rhino to pick up the new settings."
      : "Imported. Start Rhino to pick up the new settings. Your previous settings were backed up.";

    Transition(State.Finished(report, success, "Import failed. Nothing was changed."));
  }

  private static string EnsureExtension(string fileName)
    => fileName.EndsWith(RhsArchive.Extension, StringComparison.OrdinalIgnoreCase)
      ? fileName
      : fileName + RhsArchive.Extension;

  private static string ToolVersion
    => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

}
