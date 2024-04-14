namespace ModPlayer.Models;

/// <summary>
/// RowData stores all the information for a single row. If there are 8 tracks in this mod then this
/// structure will be filled with 8 elements of NoteData, one for each track.
/// </summary>
public record RowData
{
    public NoteData[] Note { get; set; } = null!;
}
