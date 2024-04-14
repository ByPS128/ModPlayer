namespace ModPlayer.Models;

/// <summary>
///     TrackData is used to store ongoing information about a particular track.
/// </summary>
public record TrackData
{
    public int Period_tuned;

    /// <summary>
    ///     The current instrument being played (0 for none)
    /// </summary>
    public int InstrumentNumber{ get; set; } 

    /// <summary>
    ///     The current playback position in the instrument, stored in fixed-point format
    /// </summary>
    public int InInstrumentPosition{ get; set; } 

    /// <summary>
    ///     Which note to play, stored as an index into the PeriodTable array
    /// </summary>
    public int PeriodIndex{ get; set; }  

    /// <summary>
    ///     The period number that period_index corresponds to (needed for various effects)
    /// </summary>
    public int Period{ get; set; } 

    /// <summary>
    ///     This is the actual frequency used to do the mixing. It's a combination of the value
    ///     calculated from the period member and an "adjustment" made by various effects.
    /// </summary>
    public float FrequencyInHz{ get; set; } 

    /// <summary>
    ///     Volume that this track is to be mixed at
    /// </summary>
    public int Volume { get; set; }

    /// <summary>
    ///     This is the actual volume used to do the mixing. It's a combination of the volume
    ///     member and an "adjustment" made by various effects.
    /// </summary>
    public int MixVolume { get; set; }

    /// <summary>
    ///     Used by the porta effect, this stores the note we're porta-ing (?) to
    /// </summary>
    public int Porta { get; set; }

    /// <summary>
    ///     The speed at which to porta
    /// </summary>
    public int PortaSpeed { get; set; }

    /// <summary>
    ///     Vibrato speed
    /// </summary>
    public int VibratoSpeed { get; set; }

    /// <summary>
    ///     Vibrato depth
    /// </summary>
    public int VibratoDepth { get; set; }

    /// <summary>
    /// The speed to turn the volume up.
    /// </summary>
    public int VibratoSlideUp { get; set; }

    /// <summary>
    /// The speed to turn the volume down.
    /// </summary>
    public int VibratoSlideDown { get; set; }

    /// <summary>
    ///     Tremolo speed
    /// </summary>
    public int TremoloSpeed { get; set; }

    /// <summary>
    ///     Tremolo depth
    /// </summary>
    public int TremoloDepth { get; set; }

    /// <summary>
    ///     Pan value....this player doesn't actually do panning, so this member is ignored
    /// </summary>
    public int PanValue { get; set; }

    /// <summary>
    ///     These next two values are pointers to the sine table. They're used to do
    /// </summary>
    public int SinePosition { get; set; }

    /// <summary>
    ///     various effects.
    /// </summary>
    public int SineNegativeFlag { get; set; }

    /// <summary>
    /// Tremolo wave form control.
    /// </summary>
    public int  TremoloWaveForm { get; set; }

    /// <summary>
    /// Vibrato wave form control.
    /// </summary>
    public int  VibratoWaveForm { get; set; }

    /// <summary>
    /// The position within the postern indicating the mark where to jump.
    /// </summary>
    public int PatternLoopLabel { get; set; }

    /// <summary>
    /// The number of remaining hops to the Pattern Loop Label.
    /// </summary>
    public int PatternLoopCount { get; set; }
}
