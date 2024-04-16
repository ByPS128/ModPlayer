using System.Text;

namespace ModPlayer.SongLoaders;

public sealed class StarTrackerLoader : ModLoader
{
    public override bool CanHandle(Span<byte> songData)
    {
        if (songData.Length < 1084)
        {
            return false;
        }

        var modKind = Encoding.ASCII.GetString(songData.Slice(1080, 4));
        if (modKind is not "FLT4" and not "FLT8")
        {
            return false;
        }

        return true;
    }

    protected override void SetupBasicProperties()
    {
        _song.SourceFormat = "StarTracker";
        _song.InstrumentsCount = 32;
        _song.RowsPerPattern = 64;
        _song.OrdersCount = 128;
        _song.NumberOfTracks = _song.Mark switch
        {
            "FLT4" => 4,
            "FLT8" => 8,
            _      => throw new ApplicationException($"Unsupported StarTracker file format: '{_song.Mark}'.")
        };
    }
}
