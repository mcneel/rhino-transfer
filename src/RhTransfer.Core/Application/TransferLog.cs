using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Application;

/// <summary>
/// Collects what happened during a transfer so the caller gets one report rather than a
/// stream of exceptions. Failures are values here, not control flow.
/// </summary>
public sealed class TransferLog
{

  private List<TransferMessage> Entries { get; } = [];

  public void Info(string text) => Entries.Add(new TransferMessage(TransferSeverity.Info, text));

  public void Warn(string text) => Entries.Add(new TransferMessage(TransferSeverity.Warning, text));

  public void Error(string text) => Entries.Add(new TransferMessage(TransferSeverity.Error, text));

  public bool HasErrors => Entries.Any(e => e.Severity == TransferSeverity.Error);

  public TransferReport ToReport(int fileCount, long byteCount, string? backupPath = null) => new()
  {
    IsSuccess = !HasErrors,
    FileCount = fileCount,
    ByteCount = byteCount,
    BackupPath = backupPath,
    Messages = [.. Entries]
  };

  public TransferReport ToFailure(string reason)
  {
    Error(reason);
    return ToReport(0, 0);
  }

}
