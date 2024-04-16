using System.Text;

namespace ModPlayer.SongLoaders;

public sealed class FastTrackerLoader : ModLoader
{
    public override bool CanHandle(Span<byte> songData)
    {
        if (songData.Length < 1084)
        {
            return false;
        }

        var modKind = Encoding.ASCII.GetString(songData.Slice(1080, 4));
        if (modKind is not "4CHN" and not "6CHN" and not "8CHN" and not "12CN" and not "16CN" and not "32CN")
        {
            return false;
        }

        return true;
    }

    protected override void SetupBasicProperties()
    {
        _song.SourceFormat = "FastTracker";
        if (_song.Mark is "4CHN")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 4;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
        else if (_song.Mark is "6CHN")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 6;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
        else if (_song.Mark is "8CHN")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 8;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
        else if (_song.Mark is "12CN")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 12;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
        else if (_song.Mark is "16CN")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 16;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
        else if (_song.Mark is "32CN")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 32;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
    }
}
