using System.Buffers;
using ModPlayer.Models;
using NAudio.Wave;

namespace ModPlayer;

// This section contains support methods for the ModPlay class that do not need to be seen in the main implementation of the class.
public sealed partial class ModPlay
{
    /// <inheritdoc />
    public void JumpToOrder(int newOrderNumber)
    {
        _order = newOrderNumber;
    }

    /// <inheritdoc />
    public void TurnOnOffAllChannels(bool setOn)
    {
        for (var trackNumber = 0; trackNumber < _numberOfTracks; trackNumber++)
        {
            TurnOnOffChannel(trackNumber, setOn);
        }
    }

    /// <inheritdoc />
    public void TurnOnOffChannel(int trackNumber, bool setOn)
    {
        if (trackNumber < 0 || trackNumber >= _numberOfTracks)
        {
            throw new ArgumentOutOfRangeException(nameof(trackNumber), "Invalid track number");
        }

        _channels[trackNumber].IsOn = setOn;
    }

    /// <inheritdoc />
    public void SetStereoPan(int newStereoPanValue)
    {
        _stereoPanValue = newStereoPanValue;
    }

    /// <inheritdoc />
    public void SetMasterVolume(int volume)
    {
        // Initialize my volume table
        for (var i = 0; i < 65; i++)
        {
            VolumeTable[i] = new int[256];
            for (var j = 0; j < 256; j++)
            {
                var vol = j;
                if (vol >= 128)
                {
                    vol -= 255 - 1;
                }

                VolumeTable[i][j] = volume * i * vol / 64;
            }
        }
    }

    /// <inheritdoc />
    public void PrepareToPlay(int playbackFrequencyInHz, int bitsPerSample, ChannelsVariation channelsKind, int volumeLevel)
    {
        _channelsKind = channelsKind;
        var numberOfChannels = channelsKind == ChannelsVariation.Mono ? 1 : 2;

        // Set up the kind of waveform data we want
        WaveFormat = WaveFormat.CreateCustomFormat(
            WaveFormatEncoding.Pcm,
            playbackFrequencyInHz,
            numberOfChannels,
            numberOfChannels * playbackFrequencyInHz * bitsPerSample / 8,
            numberOfChannels * bitsPerSample / 8,
            bitsPerSample);

        _surroundSoundDelay = playbackFrequencyInHz / 44;

        SetMasterVolume(volumeLevel);
        TurnOnOffAllChannels(true);

        // Reset all track data
        for (var track = 0; track < _numberOfTracks; track++)
        {
            _trackData[track] = new TrackData();
        }

        // Get ready to play
        _speed = 6;
        _beatsPerMinute = 125;
        _tickSamplesLeft = 0;
        _order = 0;
        _row = 0;
        _currentRow = _patterns[_orders[_order]].Row[_row];
        _tick = 0;
        _patternDelay = 0; // Effect EEx

        // If we're playying in surround then clear the delay buffers
        if (_channelsKind == ChannelsVariation.Surround)
        {
            _bufferOfLeftChannel = new int[_surroundSoundDelay];
            _bufferOfRightChannel = new int[_surroundSoundDelay];
            for (var i = 0; i < _surroundSoundDelay; i++)
            {
                _bufferOfLeftChannel[i] = 0;
                _bufferOfRightChannel[i] = 0;
            }
        }
    }

    /// <inheritdoc />
    public void DescribeSong(Action<string, string?>? callback)
    {
        if (callback is null)
        {
            return;
        }

        callback("Title", _songName);
        callback("Kind", _modKind);
        for (int i = 1; i < _instrumentsCount; i++)
        {
            callback($"instrument {i:X2}", _instruments[i]?.Name);
        }
    }

    /// <inheritdoc />
    public void WriteWaveData(Stream waveOut, int milliseconds)
    {
        var bufferBytesPerPosition = (WaveFormat.BitsPerSample == 8 ? 1 : 2) * WaveFormat.Channels;
        var totalBytesNeeded = WaveFormat.SampleRate * milliseconds * bufferBytesPerPosition / 2 * 2;

        var bufferLength = WaveFormat.SampleRate / 8; // Buffer size for 1/8 second of audio
        var doubleBufferLength = bufferLength * WaveFormat.Channels;
        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(doubleBufferLength); // 2 bytes per sample
        try
        {
            var bytesGenerated = 0;
            while (bytesGenerated < totalBytesNeeded)
            {
                var bytesToGenerate = Math.Min(buffer.Length, totalBytesNeeded - bytesGenerated);
                var bytesRead = Read(buffer, 0, bytesToGenerate);
                waveOut.Write(buffer, 0, bytesRead);
                bytesGenerated += bytesRead;
            }
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    /// <inheritdoc />
    public void WriteInstrumentsToFiles()
    {
        if (_instrumentsCount <= 0)
        {
            return;
        }

        var waveFormat = new WaveFormat(22050, 8, 1);
        for (var i = 1; i < _instrumentsCount; i++)
        {
            if (_instruments[i].Length <= 0)
            {
                continue;
            }

            var instrumentFileName = Path.Combine("C:\\temp\\", Path.GetFileName(_modFileName) + $"-instrument{i:D2}.wav");
            using var waveOut = new WaveFileWriter(instrumentFileName, waveFormat);
            var instrumentRawData = new byte[_instruments[i].Length - 1];
            for (var j = 0; j < _instruments[i].Length - 1; j++)
            {
                // Invert the instrument samples and copy into the instrumentRawData array
                instrumentRawData[j] = (byte) (_instruments[i].Data[j] ^ 0x80);
            }

            waveOut.Write(instrumentRawData, 0, instrumentRawData.Length);
            waveOut.Close();
        }
    }

    // /// <inheritdoc />
    // public long CalculateNoLoopingLengthInMs()
    // {
    //     int totalTicks = 0;
    //     HashSet<int> visitedPatterns = new HashSet<int>();
    //     int currentPatternIndex = 0;
    //     bool isLoopDetected = false;
    //
    //     while (currentPatternIndex < _patterns.Count && !isLoopDetected)
    //     {
    //         if (visitedPatterns.Contains(currentPatternIndex))
    //         {
    //             // Loop detected, we break out as no looping length is requested
    //             break;
    //         }
    //
    //         visitedPatterns.Add(currentPatternIndex);
    //         var pattern = _patterns[currentPatternIndex];
    //         for (int rowIndex = 0; rowIndex < pattern.Rows.Count; rowIndex++)
    //         {
    //             var row = pattern.Rows[rowIndex];
    //             totalTicks += _speed;
    //
    //             foreach (var channel in row.Channels)
    //             {
    //                 foreach (var effect in channel.Effects)
    //                 {
    //                     switch (effect.Type)
    //                     {
    //                         case EffectType.SetSpeed:
    //                             _speed = effect.Parameter;
    //                             break;
    //                         case EffectType.JumpToPattern:
    //                             currentPatternIndex = effect.Parameter - 1;
    //                             rowIndex = pattern.Rows.Count;  // Break out of row loop
    //                             break;
    //                         case EffectType.PatternBreak:
    //                             rowIndex = pattern.Rows.Count;  // Finish current pattern
    //                             break;
    //                     }
    //                 }
    //             }
    //         }
    //
    //         currentPatternIndex++;
    //     }
    //
    //     return ConvertTicksToMilliseconds(totalTicks, _bpm);
    // }
    //
    // private int GetNextPatternIndex(int currentPatternIndex, Row row)
    // {
    //     // This should incorporate logic to handle pattern navigation based on effects in the row
    //     return currentPatternIndex + 1;  // Simplified for demonstration
    // }
    //
    // private long ConvertTicksToMilliseconds(int ticks, int bpm)
    // {
    //     double ticksPerMinute = bpm * _ticksPerRow * 60;
    //     return (long)((ticks / ticksPerMinute) * 1000);
    // }

    /// <summary>
    ///     16-bit word values in a mod are stored in the Motorola Most-Significant-Byte-First format. They're also
    ///     stored at half their actual value, thus doubling their range. This function accepts a pointer to such a
    ///     word and returns it's integer value.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    private int ReadAmigaWord(Span<byte> data, ref int index)
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

    private int Clip(int value, int lowClip, int hiClip)
    {
        return value < lowClip ? lowClip : value > hiClip ? hiClip : value;
    }

    /// <summary>
    ///     Creates a quick seek and get index by period.
    /// </summary>
    /// <returns>Constructed index instance</returns>
    private Dictionary<int, int>? CreatePeriodIndex()
    {
        var index = new Dictionary<int, int>();
        for (var i = 0; i < ProTrackerPeriods.Length; i++)
        {
            index[ProTrackerPeriods[i]] = i;
        }

        return index;
    }

    private void Write8bitSamplesToBuffer(Span<int> local8BitBuffer, Span<byte> outputWaveBuffer)
    {
        // I will convert the float samples to 16-bit integers and then to bytes
        for (var i = 0; i < local8BitBuffer.Length; i++)
        {
            var sample = local8BitBuffer[i];
            outputWaveBuffer[i] = (byte) (sample & 0xFF);
        }
    }

    private void Write16bitSamplesToBuffer(Span<int> local16BitBuffer, Span<byte> outputWaveBuffer)
    {
        // I will convert the int samples to 16-bit integers and then to bytes
        for (var i = 0; i < local16BitBuffer.Length; i++)
        {
            var sample = Convert.ToInt16(local16BitBuffer[i]);
            outputWaveBuffer[2 * i] = (byte) (sample & 0xFF);
            outputWaveBuffer[2 * i + 1] = (byte) ((sample >> 8) & 0xFF);
        }
    }

    /// <summary>
    ///     This method converts Amiga frequency (period) to frequency in Hz
    /// </summary>
    private static float Period2Frequency(float period)
    {
        if (period == 0)
        {
            return 0;
        }

        return 7027730.742134f / (period * 2);
    }
}
