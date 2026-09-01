namespace RhTransfer.Core.Domain;

/// <summary>
/// Why a Rhino version is worth offering. A version nobody has installed and that holds no
/// settings is simply not listed, so there is no "neither" case.
/// </summary>
public enum RhinoAvailability
{
  /// <summary>Installed and already has settings. Can be exported from and imported into.</summary>
  Configured,

  /// <summary>Installed but never started, so it has no settings yet. Import only.</summary>
  InstalledWithoutSettings,

  /// <summary>Settings are here but the application was not found, e.g. after an uninstall.</summary>
  SettingsWithoutApplication
}
