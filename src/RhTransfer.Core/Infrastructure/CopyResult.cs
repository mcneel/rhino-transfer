namespace RhTransfer.Core.Infrastructure;

public readonly record struct CopyResult(int FileCount, long ByteCount)
{
  public static CopyResult operator +(CopyResult a, CopyResult b)
    => new(a.FileCount + b.FileCount, a.ByteCount + b.ByteCount);
}
