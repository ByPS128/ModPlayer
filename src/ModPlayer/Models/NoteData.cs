namespace ModPlayer.Models;

/// <summary>
/// The NoteData structure stores the information for a single note and/or effect.
/// </summary>
public record NoteData
{
    /// <summary>
    ///     The sample number (ie "instrument") to play, 1->31
    /// </summary>
    public int InstrumentNumber { get; set; }

    /// <summary>
    /// Period number, see frequency tables UNIC_Period and PT_Period.
    /// </summary>
    public int Period { get; set; }

    /// <summary>
    ///     This effectively stores the frequency to play the sample, although it's actually an index into PeriodTable.
    /// </summary>
    public int PeriodIndex { get; set; }

    /// <summary>
    ///     Contains the effect code
    /// </summary>
    public int Effect { get; set; }

    /// <summary>
    ///     Used to store control parameters for the various effects
    /// </summary>
    public int EffectParameters { get; set; }
}
