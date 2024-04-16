using System.Text;

namespace ModPlayer.SongLoaders;

public sealed class OctalyzerLoader : ModLoader
{
    public override bool CanHandle(Span<byte> songData)
    {
        if (songData.Length < 1084)
        {
            return false;
        }

        var modKind = Encoding.ASCII.GetString(songData.Slice(1080, 4));
        if (modKind is not "OKTA" and not "CD81")
        {
            return false;
        }

        return true;
    }

    protected override void SetupBasicProperties()
    {
        _song.SourceFormat = "Octalyzer";
        if (_song.Mark is "OKTA" or "CD81")
        {
            _song.InstrumentsCount = 32;
            _song.NumberOfTracks = 8;
            _song.RowsPerPattern = 64;
            _song.OrdersCount = 128;
        }
    }
}
