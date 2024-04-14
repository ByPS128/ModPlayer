namespace ModPlayer.Models;

/// <summary>
/// Pattern contains all the information for a single pattern. It is filled with 64 elements of type RowData.
/// </summary>
public record Pattern
{
      public RowData[] Row { get; set; }
}
