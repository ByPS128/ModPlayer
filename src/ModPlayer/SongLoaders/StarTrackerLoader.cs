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
        if (_song.Mark is "FLT4")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 4;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
        else if (_song.Mark is "FLT8")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 8;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
    }
}
