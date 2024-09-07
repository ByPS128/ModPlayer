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
        for (var trackNumber = 0; trackNumber < _song.NumberOfTracks; trackNumber++)
        {
            _song.TurnOnOffChannel(trackNumber, setOn);
        }
    }

    /// <inheritdoc />
    public void SetStereoPan(int newStereoPanValue)
    {
        _stereoPanValue = newStereoPanValue;
    }

    public void SetEqualizer(int numberOfBands)
    {
        if (numberOfBands <= 0)
        {
            NumberOfBands = 0;
            Equalizer = null;

            return;
        }

        NumberOfBands = numberOfBands;
        Equalizer = new Equalizer(WaveFormat.SampleRate, numberOfBands);
    }
    
    /// <inheritdoc />
    public void PrepareToPlay(Song song, int playbackFrequencyInHz, int bitsPerSample, ChannelsVariation channelsKind, int volumeLevel)
    {
        _song = song;
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
        _trackData = new TrackData[_song.NumberOfTracks];
        for (var track = 0; track < _song.NumberOfTracks; track++)
        {
            _trackData[track] = new TrackData();
        }

        // Get ready to play
        _speed = 6;
        _beatsPerMinute = 125;
        _tickSamplesLeft = 0;
        _order = 0;
        _row = 0;
        _currentRow = _song.Patterns[_song.Orders[_order]].Row[_row];
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

    private int Clip(int value, int lowClip, int hiClip)
    {
        return value < lowClip ? lowClip : value > hiClip ? hiClip : value;
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
