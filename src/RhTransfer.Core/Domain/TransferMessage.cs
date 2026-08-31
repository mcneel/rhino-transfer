namespace RhTransfer.Core.Domain;

public sealed record TransferMessage(TransferSeverity Severity, string Text)
{
  public override string ToString() => Severity switch
  {
    TransferSeverity.Info => Text,
    TransferSeverity.Warning => $"warning: {Text}",
    TransferSeverity.Error => $"error: {Text}"
  };
}
