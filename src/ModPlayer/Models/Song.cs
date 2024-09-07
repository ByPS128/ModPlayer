namespace ModPlayer.Models;

public record Song
{
    public string Mark { get; internal set; }
    public string Name { get; internal set; }
    public int NumberOfTracks { get; internal set; }
    public int RowsPerPattern { get; internal set; }
    public int InstrumentsCount { get; internal set; }
    public int OrdersCount { get; internal set; }
    public TrackData[] TrackData { get; internal set; }
    public ChannelData[] Channels { get; internal set; }
    public Instrument[] Instruments { get; internal set; }
    public int Length { get; internal set; }
    public int PatternsCount { get; internal set; }
    public int[] Orders { get; internal set; }
    public Pattern[] Patterns { get; internal set; }
    public string? SourceFileName { get; internal set; }
    public string SourceFormat { get; internal set; }

    public void SetSourceFileName(string songFileName)
    {
        SourceFileName = songFileName;
    }

    /// <inheritdoc />
    public void TurnOnOffChannel(int trackNumber, bool setOn)
    {
        if (trackNumber < 0 || trackNumber >= NumberOfTracks)
        {
            throw new ArgumentOutOfRangeException(nameof(trackNumber), "Invalid track number");
        }

        Channels[trackNumber].IsOn = setOn;
    }

    /// <inheritdoc />
    public void DescribeSong(Action<string, string?>? callback)
    {
        if (callback is null)
        {
            return;
        }

        callback("Title", Name);
        callback("Mark", Mark);
        callback("Source", SourceFormat);
        for (int i = 1; i < InstrumentsCount; i++)
        {
            callback($"instrument {i:X2}", Instruments[i]?.Name);
        }
    }
}
