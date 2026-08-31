using System;

namespace RhTransfer.Core.Domain;

/// <summary>
/// A restore point sitting beside the data folder, named "&lt;name&gt;_backup_yyyy-MM-dd-HH-mm-ss".
/// The naming matches Rhino 9's own so that backups made here appear in its Migration Assistant.
/// </summary>
public sealed record BackupFolder(string Path, string Name, DateTime CreatedOn)
{
  public override string ToString() => $"{Name} ({CreatedOn:yyyy-MM-dd HH:mm:ss})";
}
