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
        _song.InstrumentsCount = 32;
        _song.RowsPerPattern = 64;
        _song.OrdersCount = 128;
        _song.NumberOfTracks = _song.Mark switch
        {
            "4CHN" => 4,
            "6CHN" => 6,
            "8CHN" => 8,
            "12CN" => 12,
            "16CN" => 16,
            "32CN" => 32,
            _      => throw new ApplicationException($"Unsupported FastTracker file format: '{_song.Mark}'.")
        };
    }
}
