namespace RhTransfer.Cli;

public sealed record CommandLineOptions
{
  public required Verb Verb { get; init; }
  public int? Major { get; init; }
  public string? File { get; init; }
  public string? Backup { get; init; }
  public string? DataRoot { get; init; }
  public bool IsDryRun { get; init; }
}
