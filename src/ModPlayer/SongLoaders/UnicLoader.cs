using System.Text;
using ModPlayer.Models;

namespace ModPlayer.SongLoaders;

/// <summary>
/// UNIC Tracker 2 - Created by Anders E. Hansen (Laxity / Kefrens) 1991-1993.
/// Sometimes called UNIC Tracker 2, a module packer created by Anders Emil Hansen (Laxity), used in Kefrens demos.
/// </summary>
public sealed class UnicLoader : Loaderbase, ISongLoader
{
    // Frequency table for UNIC format.
    // 5 octaves
    // UNIC Tracker does not store periods in the music file, but only indey do
    // frequency tables. That's why I have a conversion table here that is already
    // compatible with ProTracker frequency table.
    private static ushort[] UnicPeriods { get; } = new ushort[61] {
        0,
        856, 808, 762, 720, 678, 640, 604, 570, 538, 508, 480, 453, // C-1 až B-1
        428, 404, 381, 360, 339, 320, 302, 285, 269, 254, 240, 226, // C-2 až B-2
        214, 202, 190, 180, 170, 160, 151, 143, 135, 127, 120, 113, // C-3 až B-3
        107, 101, 95, 90, 85, 80, 75, 71, 67, 63, 60, 56, // C-4 až B-4
        53, 50, 47, 45, 42, 40, 37, 35, 33, 31, 30, 28 // C-5 až B-5
    };

    public override bool CanHandle(Span<byte> songData)
    {
        return true;
    }

    public override Song Load(Span<byte> songData)
    {
        if (CanHandle(songData) is false)
        {
            throw new ApplicationException("Unsupported UNIC file format.");
        }

        var index = 0;
        // _song.Name = Encoding.ASCII.GetString(songData.Slice(index, 20));
        // index += 20;

        _song.TrackData = new TrackData[_song.NumberOfTracks];
        SetupBasicProperties();
        SetupChannels();
        ParseInstruments(songData, ref index);
        ReadSongData(songData, ref index);
        //index++; // Skip over this byte, it's no longer used
        ParsePatterns(songData, ref index);
        LoadInstruments(songData, index);

        return _song;
    }

    private void SetupChannels()
    {
        _song.Channels = new ChannelData[_song.NumberOfTracks];
        for (var i = 0; i < _song.NumberOfTracks; i++)
        {
            _song.Channels[i] = new ChannelData();
        }
    }

    private void SetupBasicProperties()
    {
        _song.SourceFormat = "Laxity Tracker (Kefrens)";
        _song.NumberOfTracks = 4;
        _song.RowsPerPattern = 64;
        _song.InstrumentsCount = 32;
        _song.OrdersCount = 128;
    }

    private void ParseInstruments(Span<byte> songData, ref int index)
    {
        // Read in all the instrument headers - instrument #0 is ignored !
        _song.Instruments = new Instrument[_song.InstrumentsCount];
        for (var i = 1; i < _song.InstrumentsCount; i++)
        {
            // Read the instrument name
            _song.Instruments[i] = new Instrument();
            _song.Instruments[i].Name = Encoding.ASCII.GetString(songData.Slice(index, 20));
            index += 20;
    
            // Read remaining info about instrument
            _song.Instruments[i].FineTune = -((short) ReadAmigaWord(songData, ref index) / 2);
            _song.Instruments[i].Length = ReadAmigaWord(songData, ref index);
            index++; // Skip one useless byte
            _song.Instruments[i].Volume = songData[index++];
            _song.Instruments[i].LoopStart = ReadAmigaWord(songData, ref index);
            _song.Instruments[i].LoopLength = ReadAmigaWord(songData, ref index);
            _song.Instruments[i].LoopEnd = _song.Instruments[i].LoopStart + _song.Instruments[i].LoopLength;
    
            // Fix loop end in case it goes too far
            if (_song.Instruments[i].LoopEnd > _song.Instruments[i].Length)
            {
                _song.Instruments[i].LoopEnd = _song.Instruments[i].Length;
            }
        }
    }

    private void ReadSongData(Span<byte> modFileDataSpan, ref int index)
    {
        // Read in song data
        _song.Length =  modFileDataSpan[index++];
        index++; // Skip over this byte, it's no longer used
        _song.PatternsCount = 0;
        _song.Orders = new int[_song.OrdersCount];
        for (var i = 0; i < _song.OrdersCount; i++)
        {
            _song.Orders[i] = modFileDataSpan[index++];
            _song.PatternsCount = Math.Max(_song.PatternsCount, _song.Orders[i]);
        }
    
        _song.PatternsCount++;
    }

    private void ParsePatterns(Span<byte> modFileDataSpan, ref int index)
    {
        // Load in the pattern data
        _song.Patterns = new Pattern[_song.PatternsCount];
        for (var pattern = 0; pattern < _song.PatternsCount; pattern++)
        {
            // Set the number of rows for this pattern, for mods it's always 64
            _song.Patterns[pattern] = new Pattern();
            _song.Patterns[pattern].Row = new RowData[_song.RowsPerPattern];
    
            // Loop through each row
            for (var row = 0; row < _song.RowsPerPattern; row++)
            {
                // Set the number of notes for this pattern
                _song.Patterns[pattern].Row[row] = new RowData();
                _song.Patterns[pattern].Row[row].Note = new NoteData[_song.NumberOfTracks];

                ParseRowNotes(modFileDataSpan, _song.Patterns[pattern].Row[row].Note, ref index);
            }
        }
    }

    protected void ParseRowNotes(Span<byte> modFileDataSpan, NoteData[] notes, ref int index)
    {
        // Loop through each note
        for (var note = 0; note < _song.NumberOfTracks; note++)
        {
            notes[note] = ParseTrackNote(modFileDataSpan, ref index);
        }
    }

    protected NoteData ParseTrackNote(Span<byte> modFileDataSpan, ref int index)
    {
        /* Source: http://membres.multimania.fr/asle/AMPD_src/UNIC_Tracker2.html
                Sample number
                 /         | \
                |  b0      |  | b1        b2
                0000 0000  0000 0000  0000 0000
                | \     /       \  /  \       /
                | relative     effect   effect
                |  note                 value
                | number
                |
                \
               unused
        */

        var noteData = new NoteData();

        // Get the 4 bytes for this note
        int b0 = modFileDataSpan[index++];
        int b1 = modFileDataSpan[index++];
        int b2 = modFileDataSpan[index++];
                    
        // Parse them
        noteData.InstrumentNumber = ((b0 & 0x40) >> 2) | ((b1 & 0xF0) >> 4);
        if (noteData.InstrumentNumber > _song.InstrumentsCount)
        {
            noteData.PeriodIndex = -1;
        }

        var period = b0 & 0x3F;
        if (period > 0)
        {
            if (period >= UnicPeriods.Length)
            {
            }

            noteData.Period = UnicPeriods[period];
            if (SongConstants.PeriodsIndex.TryGetValue(noteData.Period, out var periodIndex))
            {
                noteData.PeriodIndex = periodIndex;
            }
            else
            {
                Console.WriteLine($"Period {period} not found in the index.");
                noteData.PeriodIndex = -1;
            }
        }

        noteData.Effect = b1 & 0x0F;
        noteData.EffectParameters = b2;
        noteData.EffectParameterX = b2 >> 4;
        noteData.EffectParameterY = b2 & 0x0F;

        return noteData;
    }

    private void LoadInstruments(Span<byte> modFileDataSpan, int index)
    {
        // Load in the instrument data
        for (var i = 1; i < _song.InstrumentsCount; i++)
        {
            if (_song.Instruments[i].Length == 0)
            {
                continue;
            }
    
            var sourceSpan = modFileDataSpan.Slice(index, _song.Instruments[i].Length);
            var instrumentRawData = new byte[_song.Instruments[i].Length + 1]; // Allocate extra byte for anti-aliasing
            sourceSpan.CopyTo(instrumentRawData); 
            instrumentRawData[_song.Instruments[i].Length] = instrumentRawData[_song.Instruments[i].Length - 1];    
            _song.Instruments[i].Data = instrumentRawData; // Assign the processed data back to the instrument

            // Correct the loop end if needed.
            if (_song.Instruments[i].LoopEnd > _song.Instruments[i].Length)
            {
                _song.Instruments[i].LoopEnd = _song.Instruments[i].Length;
            }
   
            index += _song.Instruments[i].Length;
        }
    }
}
