using ModPlayer.Models;

namespace ModPlayer.SongLoaders;

public abstract class Loaderbase : ISongLoader
{
    protected  readonly Song _song;

    public abstract bool CanHandle(Span<byte> songData);

    public abstract Song Load(Span<byte> songData);

    public Loaderbase()
    {
        _song = new Song();
    }

    /// <summary>
    ///     16-bit word values in a mod are stored in the Motorola Most-Significant-Byte-First format. They're also
    ///     stored at half their actual value, thus doubling their range. This function accepts a pointer to such a
    ///     word and returns it's integer value.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    protected static int ReadAmigaWord(Span<byte> data, ref int index)
    {
        // .net variation to solve the same problem, but it looks that is not so clear and do lots internals calls.
        // so I will keep the simplest way to do it.
        // int value = BinaryPrimitives.ReadInt16BigEndian(data.Slice(index, 2));
        // index += 2;
        // return value * 2;

        int byte1 = data[index++];
        int byte2 = data[index++];
        return (byte1 * 256 + byte2) * 2;
    }
}
