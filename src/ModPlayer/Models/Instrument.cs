namespace ModPlayer.Models;

/// <summary>
/// The Sample structure is used to store all the information for a single sample, or "instrument". Most of the
/// member's should be self-explanatory. The "data" member is an array of bytes containing the raw sample data
/// in 8-bit signed format.
/// </summary>
public record Instrument
{
        public string Name { get; set; }

        public int Length { get; set; }

        public int FineTune { get; set; }

        public int Volume { get; set; }

        public int LoopStart { get; set; }

        public int LoopLength { get; set; }

        public int LoopEnd { get; set; }

        public byte[] Data { get; set; }
}
