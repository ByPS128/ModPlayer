using NAudio.Wave;

namespace ModPlayer;

public interface IModPlayer
{
    bool ResamplingEnabled { get; set; }

    WaveFormat WaveFormat { get; }

    void Play();

    void Stop();

    /// <summary>
    ///     Sets the mixing of the left channel to the right and vice versa according to the weight of the stereo pan value,
    ///     which is in the range 0..100.
    /// </summary>
    /// <param name="newStereoPanValue">Range 0..100</param>
    void SetStereoPan(int newStereoPanValue);

    void TurnOnOffChannel(int trackNumber, bool setOn);

    void TurnOnOffAllChannels(bool setOn);

    void JumpToOrder(int newOrderNumber);

    /// <summary>
    ///     Sets the master volume for the mod being played. The value should be from 0 (silence) to 64 (max volume).
    /// </summary>
    /// <param name="volume"></param>
    void SetMasterVolume(int volume);

    void PrepareToPlay(int playbackFrequencyInHz, int bitsPerSample, ChannelsVariation channelsKind, int volumeLevel);

    void WriteInstrumentsToFiles();

    void WriteWaveData(Stream waveOut, int milliseconds);
}
