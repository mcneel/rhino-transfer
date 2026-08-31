namespace RhTransfer.Core.Domain;

/// <summary>
/// Where a toolbar file referenced by a window layout lives. Each case rewrites to a different
/// archive-relative shape on export and resolves against a different folder on import.
/// </summary>
public enum RuiLocation
{
  /// <summary>Under the data folder's UI directory, e.g. default.rui.</summary>
  DefaultUi,

  /// <summary>Inside an installed plug-in's folder under Plug-ins.</summary>
  PlugIn,

  /// <summary>Inside a Yak package under the shared packages folder.</summary>
  Package,

  /// <summary>Extracted from a .rhc into settings/Scheme__*/imported_rui_files.</summary>
  ImportedRui,

  /// <summary>Anywhere else on the machine, e.g. the user's Desktop or Downloads.</summary>
  External
}
