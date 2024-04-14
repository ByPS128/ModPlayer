using System.Buffers;
using ModPlayer.Models;
using NAudio.Wave;

namespace ModPlayer;

// This pb file contains the most important parts of the code related to playing mod files.
public sealed partial class ModPlay : IModPlayer, IWaveProvider, IDisposable
{
    /// <summary>
    ///     As a sample is being mixed into the buffer it's position pointer is updated. The position pointer
    ///     is a 32-bit integer that is used to store a fixed-point number. The following constant specifies
    ///     how many of the bits should be used for the fractional part.
    /// </summary>
    private const int FractionalBits = 10;

    private const int SinusTable64Low = 0;
    private const int SinusTable64High = 64;

    private const int VolumeMin = 0;
    private const int VolumeMax = 64;

    private readonly Dictionary<int, int> _periodsIndex;
    private int _beatsPerMinute; // Beats-per-minute...controls length of each tick
    private int[] _bufferOfLeftChannel; // Used to delay the left  tracks when they're played to the right channel
    private int[] _bufferOfRightChannel; // Used to delay the right tracks when they're played to the left channel
    private ChannelData[] _channels = null!;
    private ChannelsVariation _channelsKind;
    private RowData _currentRow; // Pointer to the current row being played
    private Instrument[] _instruments; // Array of instruments used in this mod
    private int _instrumentsCount; // Number of instruments in this mod
    private bool _isCurrentlyPlaying; // Set to true when a mod is being played

    private string _modFileName; // Name of the mod
    private string _songName; // Name of song, read from the file

    private int _numberOfTracks; // Number of tracks in this mod
    private int _order; // Current order being played
    private int[] _orders; // Array of orders in the song
    private int _ordersCount;
    private int _patternDelay; // The number of repetitions of the same row of the pattern.
    private Pattern[] _patterns; // Array of patterns in the song
    private int _patternsCount; // Number of patterns in this mod
    private int _row; // Current row being played
    private int _rowsCount;
    private int _songLength; // Number of orders in the song
    private int _speed; // Speed of mod being played
    private int _stereoPanValue;

    /// <summary>
    ///     Surround sound is accomplished in part by mixing each channel into the other channel with a small
    ///     delay. The DELAY constant determines how many samples long that delay should be. I keep this
    ///     delay value fixed at about 23ms. Play around with it to get different effects.
    /// </summary>
    private int _surroundSoundDelay;

    private int _tick; // Current tick number (there are "_speed" ticks between each row)
    private int _tickSamplesLeft; // Number of samples left to mix for the current tick
    private TrackData[] _trackData = null!; // Stores info for each track being played
    private WaveOutEvent? _waveEvent; // The WaveOutEvent object used to play the mod

    public ModPlay()
    {
        _periodsIndex = CreatePeriodIndex();
    }

    public bool ResamplingEnabled { get; set; } = false;

    public void Dispose()
    {
        Stop();
    }

    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    ///     NAudio will call this function when it needs more data to play
    /// </summary>
    /// <param name="buffer">Buffer into which we will write audio data</param>
    /// <param name="offset">The place from which we will record</param>
    /// <param name="count">How much will we write to</param>
    /// <returns>We return the number of bytes written</returns>
    public int Read(byte[] buffer, int offset, int count)
    {
        Console.Write(".");

        // Calculate the number of samples per tick.
        // The number of ticks is not constant and changes depending on the "speed" setting. For example, if "speed"
        // is set to 6, it means that each line of the pattern will have 6 ticks. This value can be dynamically changed
        // during the song using effects that change the playback speed.
        var samplesPerBeat = WaveFormat.SampleRate / (_beatsPerMinute * 2 / 5);

        // Calculate the number of samples per tick
        var outputBufferSize = count - offset;
        // We will mix as many samples as will fit in the buffer (bytes per sample, 8-bit audio uses 1 byte, 16-bit 2 bytes)
        var bufferBytesPerPosition = (WaveFormat.BitsPerSample == 8 ? 1 : 2) * WaveFormat.Channels;
        var samplesCount = outputBufferSize / bufferBytesPerPosition;
        var samplesBufferSize = samplesCount;

        // We use shared memory for frequently used buffers to boost performance.
        var pool = ArrayPool<int>.Shared;

        // Set up buffers for the sound to be mixed into. We'll mix into left and right channels and merge them
        // later in dependency if we need mono, stereo or surround.
        var rentedLeftArray = pool.Rent(samplesBufferSize); // Rent() may return an array larger than I ordered.
        var left = rentedLeftArray.AsSpan(0, samplesBufferSize); // This is how I make sure I'm working with a correctly sized rented array.

        var rentedRightArray = pool.Rent(samplesBufferSize);
        var right = rentedRightArray.AsSpan(0, samplesBufferSize);

        // Keep looping until we've filled the buffer
        var currentPositionInOutputBuffer = 0;
        var samplesToMix = samplesCount;
        while (samplesToMix > 0)
        {
            // Only move on to the next tick if we finished mixing the last
            if (_tickSamplesLeft == 0)
            {
                // Set the number of samples to mix in next tick chunk
                _tickSamplesLeft = samplesPerBeat;

                // If we're on tick 0 then update the row
                if (_tick == 0)
                {
                    if (_patternDelay > 0)
                    {
                        _patternDelay--;
                    }
                    else
                    {
                        // Get this row
                        _currentRow = _patterns[_orders[_order]].Row[_row];

                        // Set up for next row (effect might change these values later)
                        _row++;
                        if (_row >= 64)
                        {
                            _row = 0;
                            _order++;
                            if (_order >= _songLength)
                            {
                                _order = 0;
                            }
                        }

                        // Now update this row and set up effects
                        UpdateRow();
                    }
                }

                // Otherwise, all we gotta do is update the effects
                else
                {
                    UpdateEffects();
                }

                // Move on to next tick
                _tick++;
                if (_tick >= _speed)
                {
                    _tick = 0;
                }
            }

            // Ok, so we know that we gotta mix '_tickSamplesLeft' samples into this buffer, see how much room we actually got
            // We will use a smaller number of samples.
            var currentTickSamplesToMixCount = _tickSamplesLeft > samplesToMix ? samplesToMix : _tickSamplesLeft;

            // Make a note that we've added this amount
            _tickSamplesLeft -= currentTickSamplesToMixCount;
            samplesToMix -= currentTickSamplesToMixCount;

            // Now mix instruments samples into the right places in the buffers!
            MixTickChunk(left.Slice(currentPositionInOutputBuffer, currentTickSamplesToMixCount), right.Slice(currentPositionInOutputBuffer, currentTickSamplesToMixCount), currentTickSamplesToMixCount);
            currentPositionInOutputBuffer += currentTickSamplesToMixCount;
        }

        // At this point the sound buffers are all 16-bit signed samples. If we are playing 8-bit signed samples
        // then we need to convert them by shifting right by 8 (SAMPLE_SHIFT) and adding 128 (BIAS). We also need to
        // clip each sample to it's appropriate range. The following defines ensure that the mixing routines take
        // care of all this for us regardless of the current playback mode.
        var sampleShift = WaveFormat.BitsPerSample == 8 ? 8 : 0;
        var bias = WaveFormat.BitsPerSample == 8 ? 128 : 0;
        var lowClip = WaveFormat.BitsPerSample == 8 ? 0 : -0x8000;
        var hiClip = WaveFormat.BitsPerSample == 8 ? 255 : 0x7FFF;

        var rentedMixedOutputArray = pool.Rent(currentPositionInOutputBuffer * WaveFormat.Channels); // Rent() may return an array larger than I ordered.
        var mixedOutputBuffer = rentedMixedOutputArray.AsSpan(0, currentPositionInOutputBuffer * WaveFormat.Channels); // This is how I make sure I'm working with a correctly sized rented array.
        try
        {
            int i, j, k, l, thissample;
            switch (_channelsKind)
            {
                case ChannelsVariation.Mono:
                {
                    for (i = 0; i < currentPositionInOutputBuffer; i++)
                    {
                        thissample = ((left[i] + right[i]) >> sampleShift) + bias;
                        mixedOutputBuffer[i] = Clip(thissample, lowClip, hiClip);
                    }

                    break;
                }
                case ChannelsVariation.Stereo:
                case ChannelsVariation.StereoPan:
                {
                    for (i = 0, j = 0; i < currentPositionInOutputBuffer; i++, j += 2)
                    {
                        var leftSample = (left[i] >> sampleShift) + bias;
                        var rightSample = (right[i] >> sampleShift) + bias;

                        if (_channelsKind == ChannelsVariation.StereoPan)
                        {
                            var tempSample = leftSample;
                            leftSample = leftSample + rightSample / (_stereoPanValue / 100 + 1);
                            rightSample = rightSample + tempSample / (_stereoPanValue / 100 + 1);
                        }

                        mixedOutputBuffer[j] = Clip(leftSample, lowClip, hiClip);
                        mixedOutputBuffer[j + 1] = Clip(rightSample, lowClip, hiClip);
                    }

                    break;
                }
                case ChannelsVariation.Surround:
                {
                    for (i = 0, j = 0; i < _surroundSoundDelay; i++, j += 2)
                    {
                        thissample = ((left[i] + _bufferOfRightChannel[i]) >> sampleShift) + bias;
                        mixedOutputBuffer[j] = thissample < lowClip ? lowClip : thissample > hiClip ? hiClip : thissample;
                        thissample = ((right[i] + _bufferOfLeftChannel[i]) >> sampleShift) + bias;
                        mixedOutputBuffer[j + 1] = thissample < lowClip ? lowClip : thissample > hiClip ? hiClip : thissample;
                    }

                    for (k = 0; i < currentPositionInOutputBuffer - _surroundSoundDelay; i++, j += 2, k++)
                    {
                        thissample = ((left[i] + right[k]) >> sampleShift) + bias;
                        mixedOutputBuffer[j] = thissample < lowClip ? lowClip : thissample > hiClip ? hiClip : thissample;
                        thissample = ((right[i] + left[k]) >> sampleShift) + bias;
                        mixedOutputBuffer[j + 1] = thissample < lowClip ? lowClip : thissample > hiClip ? hiClip : thissample;
                    }

                    for (l = 0; i < currentPositionInOutputBuffer; i++, j += 2, k++, l++)
                    {
                        thissample = ((left[i] + right[k]) >> sampleShift) + bias;
                        mixedOutputBuffer[j] = thissample < lowClip ? lowClip : thissample > hiClip ? hiClip : thissample;
                        _bufferOfRightChannel[l] = right[i];
                        thissample = ((right[i] + left[k]) >> sampleShift) + bias;
                        mixedOutputBuffer[j + 1] = thissample < lowClip ? lowClip : thissample > hiClip ? hiClip : thissample;
                        _bufferOfLeftChannel[l] = left[i];
                    }

                    break;
                }
            }

            if (WaveFormat.BitsPerSample == 8)
            {
                Write8bitSamplesToBuffer(mixedOutputBuffer, buffer.AsSpan(offset, samplesCount - samplesToMix));
            }
            else
            {
                Write16bitSamplesToBuffer(mixedOutputBuffer, buffer.AsSpan(offset, outputBufferSize));
            }
        }
        finally
        {
            // Return the rented arrays to the pool
            pool.Return(rentedLeftArray);
            pool.Return(rentedRightArray);
            pool.Return(rentedMixedOutputArray);
        }

        //return currentPositionInOutputBuffer;
        var a = currentPositionInOutputBuffer;
        return (samplesCount - samplesToMix) * bufferBytesPerPosition;
    }

    /// <summary>
    ///     This function causes the mod to start playing. It returns true if the mod was successfully started, or false if
    ///     not.
    /// </summary>
    /// <returns></returns>
    public void Play()
    {
        // See if I'm already playing
        if (_isCurrentlyPlaying)
        {
            return;
        }

        // If our device is already open then close it
        Stop();

        // Open the playback device and start playing
        var waveOut = new WaveOutEvent();
        waveOut.Init(this);
        waveOut.Play();
        _isCurrentlyPlaying = true;
    }

    public void Stop()
    {
        _waveEvent?.Stop();
        _waveEvent?.Dispose();
        _waveEvent = null;
    }

    private void UpdateRow()
    {
        var neworder = _order;
        var newrow = _row;

        // Loop through each track
        for (var track = 0; track < _numberOfTracks; track++)
        {
            // Get note data
            var note = _currentRow.Note[track];

            // Make a copy of each value in the NoteData structure so they'll be easier to work with (less typing)
            var instrumentNumber = note.InstrumentNumber;
            var period = note.Period;
            var effect = note.Effect;
            var effectParameters = note.EffectParameters;
            var effectParameterX = effectParameters >> 4; // effect parameter x
            var effectParameterY = effectParameters & 0xF; // effect parameter y

            // Are we changing the instrument being played?
            if (instrumentNumber > 0)
            {
                _trackData[track].InstrumentNumber = instrumentNumber;
                _trackData[track].Volume = _instruments[instrumentNumber].Volume;
                _trackData[track].MixVolume = _trackData[track].Volume;
                if (effect != 3 && effect != 5)
                {
                    if (period > 0)
                    {
                        _trackData[track].InInstrumentPosition = 0;
                    }
                }
            }
            else
            {
                instrumentNumber = _trackData[track].InstrumentNumber;
            }

            // Are we changing the frequency being played?
            if (period >= 0)
            {
                if (effect != 3 && effect != 5)
                {
                    _trackData[track].InInstrumentPosition = 0;
                }

                // If not a porta effect, then set the channels frequency to the
                // looked up amiga value + or - any finetune
                if (effect != 3 && effect != 5)
                {
                    // Remember the note
                    _trackData[track].PeriodIndex = note.PeriodIndex;
                    _trackData[track].Period_tuned = ProTrackerPeriods[_trackData[track].PeriodIndex + _instruments[_trackData[track].InstrumentNumber].FineTune * 84];
                }

                // If there is no instrument number or effect then we reset the position
                if (instrumentNumber == 0 && effect == 0)
                {
                    _trackData[track].InInstrumentPosition = 0;
                }

                // Now reset a few things
                _trackData[track].TremoloSpeed = 0;
                _trackData[track].TremoloDepth = 0;
                _trackData[track].SinePosition = 0;
                _trackData[track].SineNegativeFlag = 0;
                _trackData[track].VibratoSlideUp = 0;
                _trackData[track].VibratoSlideDown = 0;
            }

            // Process any effects - need to include 1, 2, 3, 4 and A
            switch (note.Effect)
            {
                // Arpeggio
                case 0x00: 
                    break; // tick effect

                // Porta Up
                case 0x01: 
                    break; // tick effect

                // Porta Down
                case 0x02: 
                    break; // tick effect

                // Porta to Note (3) and Porta + Vol Slide (5)
                case 0x03:
                case 0x05:
                    if (instrumentNumber == 0)
                    {
                        break;
                    }

                    var noteFrequencyInHz = ProTrackerPeriods[_trackData[track].PeriodIndex + _instruments[_trackData[track].InstrumentNumber].FineTune * 84];
                    if (note.Period > 0)
                    {
                        _trackData[track].Porta = note.Period;
                    }

                    if (effectParameters > 0 && effect == 0x3)
                    {
                        _trackData[track].PortaSpeed = effectParameters;
                    }

                    break;

                // Vibrato
                case 0x04:
                    if (effectParameterX > 0)
                    {
                        _trackData[track].VibratoSpeed = effectParameterX;
                    }

                    if (effectParameterY > 0)
                    {
                        _trackData[track].VibratoDepth = effectParameterY;
                    }

                    break;

                // Vibrato + Vol Slide
                case 0x06: 
                    break; // tick effect

                // Tremolo
                case 0x07:
                    if (effectParameterX > 0)
                    {
                        _trackData[track].TremoloSpeed = effectParameterX;
                    }

                    if (effectParameterY > 0)
                    {
                        _trackData[track].TremoloDepth = effectParameterY;
                    }

                    break;

                // Pan - I make note of this for a future version, although it's not supported in the mixing yet
                case 0x08:
                    if (effectParameters == 0xa4)
                    {
                        _trackData[track].PanValue = 7;
                    }
                    else
                    {
                        _trackData[track].PanValue = (effectParameters >> 3) - 1;
                    }

                    if (_trackData[track].PanValue < 0)
                    {
                        _trackData[track].PanValue = 0;
                    }

                    break;

                // Instrument offset
                case 0x09:
                    _trackData[track].InInstrumentPosition = note.EffectParameters << (FractionalBits + 8);
                    break;

                // Volume Slide
                case 0x0A: 
                    break; // tick effect

                // Jump To Pattern
                case 0x0B:
                    neworder = note.EffectParameters;
                    if (neworder >= _songLength)
                    {
                        neworder = 0;
                    }

                    newrow = 0;

                    // // převzato z 0x0C efektu níže.
                    // _trackData[track].Volume = note.EffectParameters;
                    // _trackData[track].MixVolume = _trackData[track].Volume;
                    break;

                // Set Volume
                case 0x0C:
                    _trackData[track].Volume = note.EffectParameters;
                    _trackData[track].MixVolume = _trackData[track].Volume;
                    break;

                // Break from current pattern
                case 0x0D:
                    newrow = effectParameterX * 10 + effectParameterY;
                    if (newrow > _rowsCount) // 63
                    {
                        newrow = 0;
                    }

                    neworder = _order + 1;
                    if (neworder >= _songLength)
                    {
                        neworder = 0;
                    }

                    break;

                // Extended effects
                case 0x0E:
                    switch (effectParameterX)
                    {
                        // Set filter
                        case 0x00: 
                            break; // not supported

                        // Fine porta up
                        case 0x01:
                            _trackData[track].Period_tuned -= effectParameterY;
                            break;

                        // Fine porta down
                        case 0x02:
                            _trackData[track].Period_tuned += effectParameterY;
                            break;

                        // Glissando 
                        case 0x03: 
                            break; // not supported


                        // Set vibrato waveform
                        case 0x04:
                            _trackData[track].VibratoWaveForm = effectParameterY;
                            break; // not supported

                        // Set finetune
                        case 0x05:
                            _instruments[instrumentNumber].FineTune = effectParameterY;
                            if (_instruments[instrumentNumber].FineTune > 7)
                            {
                                _instruments[instrumentNumber].FineTune -= 16;
                            }

                            break;

                        // Pattern loop
                        case 0x6:
                            if (effectParameterY == 0)
                            {
                                // Nastavení znaèky kam skákat provedu jen pøi prvním prùchodu.

                                if (_trackData[track].PatternLoopCount < 0)
                                {
                                    _trackData[track].PatternLoopLabel = _row - 1;
                                    _trackData[track].PatternLoopCount = -1;
                                }
                            }
                            else
                            {
                                // Nastavím poèet skoku, pokud je znaèka známá.
                                if (effectParameterY > 0 && _trackData[track].PatternLoopLabel >= 0)
                                {
                                    // Nastavím počet skokù.
                                    if (_trackData[track].PatternLoopCount < 0)
                                    {
                                        _trackData[track].PatternLoopCount = effectParameterY;
                                    }

                                    // Pokud nejsou všechny skoky provedeny, vratím se zpìt na znaèku.
                                    if (_trackData[track].PatternLoopCount > 0)
                                    {
                                        _trackData[track].PatternLoopCount = _trackData[track].PatternLoopCount - 1;
                                        newrow = _trackData[track].PatternLoopLabel;
                                        // Pokudj e efekt $E6x na poslení øádce, mechanizmus skákal ma øádku znaèky ale v novém paternu.
                                        // Takto se to ošetøí.
                                        if (_row == 0)
                                        {
                                            neworder = _order - 1;
                                            _order = neworder;
                                        }
                                    }
                                    else
                                        // Reset po dokonèení všech skokù.
                                    if (_trackData[track].PatternLoopCount == 0)
                                    {
                                        // Neresetuji značku, zapamatuju si její poslení hodnotu.
                                        // Resetuji jen poèet skokù.
                                        _trackData[track].PatternLoopCount = -1;
                                    }
                                }
                            }

                            break;

                        // Set tremolo waveform
                        case 0x07:
                            _trackData[track].TremoloWaveForm = effectParameterY;
                            break;

                        // Pos panning -  I make note of this for a future version, 
                        // although it's not supported in the mixing yet
                        case 0x08:
                            _trackData[track].PanValue = effectParameterY;
                            break;

                        // Retrig Note
                        case 0x09: 
                            break; // tick effect

                        // Fine volside up
                        case 0x0A:
                            SlideVolume(track, effectParameterY);
                            _trackData[track].MixVolume = _trackData[track].Volume;
                            break;

                        // Fine volside down
                        case 0xB:
                            SlideVolume(track, -effectParameterY);
                            _trackData[track].MixVolume = _trackData[track].Volume;
                            break;

                        // Cut note
                        case 0x0C: 
                            break; // tick effect

                        // Delay note
                        case 0x0D:
                            _trackData[track].MixVolume = 0;
                            break;

                        // Pattern delay
                        case 0x0E:
                            _patternDelay = effectParameterY;
                            break;

                        // Invert loop
                        case 0x0F: 
                            break; // not supported
                    }

                    break;

                // Set Speed
                case 0x0F:
                    if (effectParameters < 0x20)
                    {
                        _speed = note.EffectParameters;
                    }
                    else
                    {
                        _beatsPerMinute = note.EffectParameters;
                    }

                    break;

                // If anything makes it this far then we have a problem
                default:
                    Console.WriteLine($"Oh oh! Weird effect code {note.Effect:X} at pattern {_orders[_order]} row {_row} track {track}");
                    break;
            }

            // If we have something playing then set the frequency
            if (_trackData[track].Period_tuned > 0)
            {
                _trackData[track].FrequencyInHz = Period2Frequency(_trackData[track].Period_tuned);
            }
        }

        // Update our row and orders
        _row = newrow;
        _order = neworder;
    }

    private void UpdateEffects()
    {
        // Loop through each channel
        for (var track = 0; track < _numberOfTracks; track++)
        {
            // Get note data
            var note = _currentRow.Note[track];

            // Parse it
            var effect = note.Effect; // grab the effect number
            var effectParameters = note.EffectParameters; // grab the effect parameter
            var effectParameterX = effectParameters >> 4; // grab the effect parameter x
            var effectParameterY = effectParameters & 0xF; // grab the effect parameter y

            // Process it
            switch (effect)
            {
                // Arpeggio
                case 0x00:
                    if (effectParameters > 0)
                    {
                        var period = _trackData[track].Period_tuned;
                        switch (_tick % 3)
                        {
                            case 0:
                                period = _trackData[track].Period_tuned;
                                break;
                            case 1:
                                period = ProTrackerPeriods[_trackData[track].PeriodIndex + effectParameterX + _instruments[_trackData[track].InstrumentNumber].FineTune * 84];
                                break;
                            case 2:
                                period = ProTrackerPeriods[_trackData[track].PeriodIndex + effectParameterY + _instruments[_trackData[track].InstrumentNumber].FineTune * 84];
                                break;
                        }

                        _trackData[track].FrequencyInHz = Period2Frequency(period);
                    }

                    break;

                // Porta up
                case 0x01:
                    _trackData[track].Period_tuned -= effectParameters; // subtract freq
                    _trackData[track].FrequencyInHz = Period2Frequency(_trackData[track].Period_tuned);
                    break;

                // Porta down
                case 0x02:
                    _trackData[track].Period_tuned += effectParameters; // add freq
                    _trackData[track].FrequencyInHz = Period2Frequency(_trackData[track].Period_tuned);
                    break;

                // Porta to note
                case 0x03:
                    DoPorta(track);
                    break;

                // Vibrato
                case 0x04:
                    DoVibrato(track);
                    break;

                // Porta + Vol Slide
                case 0x05:
                    DoPorta(track);
                    SlideVolume(track, effectParameterX - effectParameterY);
                    _trackData[track].MixVolume = _trackData[track].Volume;
                    break;

                // Vibrato + Vol Slide
                case 0x06:
                    DoVibrato(track);
                    _trackData[track].VibratoSlideUp = effectParameterX;
                    _trackData[track].VibratoSlideDown = effectParameterY;
                    SlideVolume(track);
                    _trackData[track].MixVolume = _trackData[track].Volume;
                    break;

                // Tremolo
                case 0x07:
                    DoTremolo(track);
                    break;

                // Pan
                case 0x08:
                    break; // note effect

                // Instrument offset
                case 0x09:
                    break; // note effect

                // Volume slide
                case 0x0A:
                    SlideVolume(track, effectParameterX - effectParameterY);
                    _trackData[track].MixVolume = _trackData[track].Volume;
                    break;

                // Jump To Pattern
                case 0x0B:
                    break; // note effect

                // Set Volume
                case 0x0C:
                    break; // note effect

                // Pattern Break
                case 0x0D:
                    break; // note effect

                // Extended effects
                case 0x0E:
                    switch (effectParameterX)
                    {
                        // Retrig note
                        case 0x9:
                            if (_tick % effectParameterY == effectParameterY - 1)
                            {
                                _trackData[track].InInstrumentPosition = 0;
                            }

                            break;

                        // Cut note
                        case 0xC:
                            if (_tick == effectParameterY)
                            {
                                _trackData[track].Volume = 0;
                                _trackData[track].MixVolume = _trackData[track].Volume;
                                _trackData[track].InInstrumentPosition = 0;
                            }

                            break;

                        // Delay note
                        case 0xD:
                            if (_tick == effectParameterY)
                            {
                                _trackData[track].MixVolume = _trackData[track].Volume;
                                _trackData[track].InInstrumentPosition = 0;
                            }

                            break;

                        // All other Exy effects are note effects
                    }

                    break;

                // Set Speed
                case 0x0F:
                    break; // note effect
            }
        }
    }

    private void DoTremolo(int track)
    {
        if (_trackData[track].TremoloSpeed == 0)
        {
            return;
        }

        var sineIndex = (_trackData[track].SinePosition & (SinusTable64High - SinusTable64Low)) + SinusTable64Low;
        var vibratoAdjustment = Clip((_trackData[track].TremoloDepth * SineTable64[sineIndex]) >> 6, VolumeMin, VolumeMax); // div64

        _trackData[track].MixVolume = vibratoAdjustment;
        _trackData[track].SinePosition += _trackData[track].TremoloSpeed;
    }

    private void DoVibrato(int track)
    {
        if (_trackData[track].VibratoSpeed == 0)
        {
            return;
        }

        var vibratoValue = 0;
        var sineIndex = (_trackData[track].SinePosition & 62) - 31;
        switch (_trackData[track].VibratoWaveForm)
        {
            case 0: // sine (default)                                       //   /\    /\     (2 cycles shown)
                vibratoValue = SineTable32[Math.Abs(sineIndex) + /*1*/ +0]; //     \/    \/
                break;

            case 1: // ramp down                      //   | \   | \
                sineIndex = Math.Abs(sineIndex) << 3; //       \ |   \ |
                if (sineIndex < 0) // ramp down
                {
                    sineIndex = 0;
                }

                if (_trackData[track].SinePosition < 0)
                {
                    sineIndex = 255 - sineIndex;
                }

                vibratoValue = sineIndex;
                break;

            case 2: // square       //    ,--,  ,--,
                vibratoValue = 255; //       '--'  '--' 
                break;

            default:
                // warning (variable might not have been initialized)
                vibratoValue = SineTable32[Math.Abs(sineIndex) + 1];
                break;
        }

        vibratoValue *= _trackData[track].VibratoDepth;
        vibratoValue >>= 7;
        if (sineIndex > 0)
        {
            // Checking due to division by zero.
            if (_trackData[track].Period_tuned + vibratoValue > 0)
            {
                _trackData[track].FrequencyInHz = Period2Frequency(_trackData[track].Period_tuned + vibratoValue);
            }
        }
        else
        {
            // Checking due to division by zero.
            if (_trackData[track].Period_tuned - vibratoValue > 0)
            {
                _trackData[track].FrequencyInHz = Period2Frequency(_trackData[track].Period_tuned - vibratoValue);
            }
        }

        // I remember the course of the vibrato.
        _trackData[track].SinePosition = _trackData[track].SinePosition % 62 + _trackData[track].VibratoSpeed;
    }

    private void DoPorta(int track)
    {
        if (_trackData[track].Porta <= 0 || _trackData[track].Period_tuned == _trackData[track].Porta)
        {
            return;
        }

        if (_trackData[track].Period_tuned < _trackData[track].Porta)
        {
            _trackData[track].Period_tuned += _trackData[track].PortaSpeed;
            if (_trackData[track].Period_tuned > _trackData[track].Porta)
            {
                _trackData[track].Period_tuned = _trackData[track].Porta;
            }
        }
        else if (_trackData[track].Period_tuned > _trackData[track].Porta)
        {
            _trackData[track].Period_tuned -= _trackData[track].PortaSpeed;
            if (_trackData[track].Period_tuned < _trackData[track].Porta)
            {
                _trackData[track].Period_tuned = _trackData[track].Porta;
            }
        }

        _trackData[track].FrequencyInHz = Period2Frequency(_trackData[track].Period_tuned);
    }

    private void SlideVolume(int track, int amount)
    {
        if (amount == 0)
        {
            return;
        }

        _trackData[track].Volume = Clip(_trackData[track].Volume + amount + amount, VolumeMin, VolumeMax);
    }

    private void SlideVolume(int track)
    {
        var slideAmount = _trackData[track].VibratoSlideUp - _trackData[track].VibratoSlideDown;
        if (slideAmount == 0)
        {
            return;
        }

        SlideVolume(track, slideAmount);
    }

    private void MixTickChunk(Span<int> leftChannelBuffer, Span<int> rightChannelBuffer, int samplesCount)
    {
        // Set up a mixing buffer and clear it,
        // Because the mixed part of non-repeating samples can be shorter than samplesCount
        // and in order to hear the silence, we have to write the silence into the buffers now.
        // The mix loop only writes the samples of the instruments that are actually being played.
        for (var i = 0; i < samplesCount; i++)
        {
            leftChannelBuffer[i] = 0;
            rightChannelBuffer[i] = 0;
        }

        // Loop through each channel and process note data
        for (var track = 0; track < _numberOfTracks; track++)
        {
            if (_channels[track].IsOn is false)
            {
                continue;
            }

            // Make sure I'm actually playing something
            if (_trackData[track].InstrumentNumber <= 0 || _trackData[track].InstrumentNumber > _instrumentsCount)
            {
                continue;
            }

            var instrumentPlayingFrequencyInHz = _trackData[track].FrequencyInHz;
            if (instrumentPlayingFrequencyInHz == 0)
            {
                continue;
            }

            // Make sure this instrument actually contains sound data
            if (_instruments[_trackData[track].InstrumentNumber].Data?.Length == 0)
            {
                continue;
            }

            // ATTENTION here, Delta tells how many samples I will move in the instrument.                                                            
            // One output channel sample does not necessarily mean one instrument sample.
            // The desired output frequency, for example 44100 Hz, and the note's pitch frequency are combined here.
            // The axiom is that the instruments are sampled at a frequency of 22KHz.
            // One sample of the instrument can thus be repeated several times, or some
            // samples of the instrument can be completely omitted.
            var deltapos = (int) (instrumentPlayingFrequencyInHz * (1 << FractionalBits) / WaveFormat.SampleRate);
            if (deltapos == 0)
            {
                continue;
            }

            var instrument = _instruments[_trackData[track].InstrumentNumber];
            var instrumentLength = instrument.Length << FractionalBits;
            var instrumentLoopStart = instrument.LoopStart << FractionalBits;
            var instrumentLoopEnd = (instrument.LoopStart + instrument.LoopLength) << FractionalBits;
            if (instrumentLoopEnd > instrumentLength)
            {
                instrumentLoopEnd = instrumentLength;
            }

            var inInstrumentPosition = _trackData[track].InInstrumentPosition;
            var trackVolumeTable = VolumeTable[_trackData[track].MixVolume];
            var mixPosition = 0;

            var mixed = (track & 3) == 0 || (track & 3) == 3 ? leftChannelBuffer : rightChannelBuffer;

            // Remaining samples to mix
            var remainingSamplesToMix = samplesCount;
            while (remainingSamplesToMix > 0)
            {
                // How many samples can we mix before we need to loop?
                int cycleSamplesToMix;

                // If I'm a looping instrument then I need to check if it's time to loop back. I also need to figure out
                // how many samples I can mix before I need to loop again
                if (instrumentLoopEnd > 2 << FractionalBits)
                {
                    if (inInstrumentPosition >= instrumentLoopEnd)
                    {
                        //position -= instrumentLoopLength;
                        inInstrumentPosition = instrumentLoopStart;
                    }

                    cycleSamplesToMix = Math.Min(remainingSamplesToMix, (instrumentLoopEnd - inInstrumentPosition - 1) / deltapos + 1);
                    if (cycleSamplesToMix < 0)
                    {
                        cycleSamplesToMix = 0;
                    }

                    remainingSamplesToMix -= cycleSamplesToMix;
                }

                // If I'm not a looping instrument then mix until I'm done playing the entire instrument
                else
                {
                    // If we've already reached the end of the instrument then forget it
                    if (inInstrumentPosition >= instrumentLength)
                    {
                        cycleSamplesToMix = 0;
                    }
                    else
                    {
                        cycleSamplesToMix = Math.Min(samplesCount, (instrumentLength - inInstrumentPosition - 1) / deltapos + 1);
                    }

                    remainingSamplesToMix = 0;
                }

                for (var i = 0; i < cycleSamplesToMix; i++)
                {
                    // Mix this sample in and update our position
                    var realInstrumentPosition = inInstrumentPosition >> FractionalBits;
                    int volumeAdjustedSampleValue;
                    if (ResamplingEnabled)
                    {
                        var sample1 = trackVolumeTable[_instruments[_trackData[track].InstrumentNumber].Data[inInstrumentPosition >> FractionalBits]];
                        var sample2 = trackVolumeTable[_instruments[_trackData[track].InstrumentNumber].Data[(inInstrumentPosition >> FractionalBits) + 1]];
                        var frac1 = inInstrumentPosition & ((1 << FractionalBits) - 1);
                        var frac2 = (1 << FractionalBits) - frac1;
                        volumeAdjustedSampleValue = (sample1 * frac2 + sample2 * frac1) >> FractionalBits;
                    }
                    else
                    {
                        var realSampleData = instrument.Data[realInstrumentPosition];
                        volumeAdjustedSampleValue = trackVolumeTable[realSampleData];
                    }

                    mixed[mixPosition++] += volumeAdjustedSampleValue;
                    inInstrumentPosition += deltapos;
                }
            }

            // Save current position
            _trackData[track].InInstrumentPosition = inInstrumentPosition;
        }
    }
}
