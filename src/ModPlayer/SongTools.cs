using ModPlayer.Models;
using NAudio.Wave;

namespace ModPlayer;

public sealed class SongTools
{
    /// <inheritdoc />
    public static void WriteInstrumentsToFiles(Song song)
    {
        if (song.InstrumentsCount <= 0)
        {
            return;
        }

        var waveFormat = new WaveFormat(22050, 8, 1);
        for (var i = 1; i < song.InstrumentsCount; i++)
        {
            if (song.Instruments[i].Length <= 0)
            {
                continue;
            }

            var baseFileName = song.SourceFileName ?? StringNormalizationExtensions.Normalize(song.Name);
            var instrumentFileName = Path.Combine("C:\\temp\\", Path.GetFileName(baseFileName) + $"-instrument{i:D2}.wav");
            using var waveOut = new WaveFileWriter(instrumentFileName, waveFormat);
            var instrumentRawData = new byte[song.Instruments[i].Length - 1];
            for (var j = 0; j < song.Instruments[i].Length - 1; j++)
            {
                // Invert the instrument samples and copy into the instrumentRawData array
                instrumentRawData[j] = (byte) (song.Instruments[i].Data[j] ^ 0x80);
            }

            waveOut.Write(instrumentRawData, 0, instrumentRawData.Length);
            waveOut.Close();
        }
    }
}

