using System.Collections.Immutable;
using System.Linq;

namespace RhTransfer.Core.Domain;

/// <summary>
/// The outcome of an export or import, returned as a value. Nothing here throws.
/// </summary>
public sealed record TransferReport
{

  public required bool IsSuccess { get; init; }

  public required int FileCount { get; init; }

  public required long ByteCount { get; init; }

  /// <summary>The restore point written before an import, when one was made.</summary>
  public string? BackupPath { get; init; }

  public required ImmutableArray<TransferMessage> Messages { get; init; }

  public ImmutableArray<TransferMessage> Errors
    => [.. Messages.Where(m => m.Severity == TransferSeverity.Error)];

  public ImmutableArray<TransferMessage> Warnings
    => [.. Messages.Where(m => m.Severity == TransferSeverity.Warning)];

}
