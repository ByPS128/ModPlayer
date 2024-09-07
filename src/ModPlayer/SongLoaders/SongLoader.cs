using ModPlayer.Models;

namespace ModPlayer.SongLoaders;

public static class SongLoader
{
    private static List<ISongLoader> _loaders = new ();

    static SongLoader()
    {
        _loaders.Add(new ModLoader());        
        _loaders.Add(new FastTrackerLoader());        
        _loaders.Add(new StarTrackerLoader());        
        _loaders.Add(new OctalyzerLoader());        
        _loaders.Add(new UnicLoader());        
    }

     public static Song LoadFromFile(string songFileName)
     {
         using var stream = File.OpenRead(songFileName);
         var song = LoadFromStream(stream);
         song.SetSourceFileName(songFileName);

         return song;
     }

     public static Song LoadFromStream(FileStream stream)
     {
         var modFileData = ReadStreamToByteArray(stream);
         return LoadFromSpan(new Span<byte>(modFileData));
     }
    
    public static Song LoadFromSpan(Span<byte> songData)
    {
        return GetLoader(songData)
            .Load(songData);
    }

    private static ISongLoader GetLoader(Span<byte> songData)
    {
        foreach (var loader in _loaders)
        {
            if (loader.CanHandle(songData))
            {
                return loader;
            }
        }

        throw new NotSupportedException("The song format is not supported.");
    }

    private static byte[] ReadStreamToByteArray(Stream stream)
    {
        var buffer = new byte[stream.Length];
        var bytesReadTotal = 0;
        var bytesToRead = buffer.Length;

        while (bytesReadTotal < bytesToRead)
        {
            var bytesRead = stream.Read(buffer, bytesReadTotal, bytesToRead - bytesReadTotal);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Could not read the stream to the end.");
            }
            bytesReadTotal += bytesRead;
        }
        return buffer;
    }

}

