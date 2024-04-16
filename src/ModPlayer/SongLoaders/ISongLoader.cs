using ModPlayer.Models;

namespace ModPlayer.SongLoaders;

public interface ISongLoader
{
    bool CanHandle(Span<byte> songData);

    Song Load(Span<byte> songData);
}

