using System.Text;
using ModPlayer.Models;

namespace ModPlayer.SongLoaders;

public abstract class Loaderbase : ISongLoader
{
    protected  readonly Song _song;

    public abstract bool CanHandle(Span<byte> songData);
    public abstract Song Load(Span<byte> songData);

    public Loaderbase()
    {
        _song = new Song();
    }
}

public class ModLoader : Loaderbase, ISongLoader
{
    public override bool CanHandle(Span<byte> songData)
    {
        if (songData.Length < 1084)
        {
            return false;
        }

        var modKind = Encoding.ASCII.GetString(songData.Slice(1080, 4));
        if (modKind is not "M.K." and not "M!K!")
        {
            return false;
        }

        return true;
    }

    public override Song Load(Span<byte> songData)
    {
        if (CanHandle(songData) is false)
        {
            throw new ApplicationException("Unsupported MOD file format.");
        }

        var index = 0;
        _song.Mark = Encoding.ASCII.GetString(songData.Slice(1080, 4));
        _song.Name = Encoding.ASCII.GetString(songData.Slice(index, 20));
        index += 20;

        SetupBasicProperties();
        _song.TrackData = new TrackData[_song.NumberOfTracks];
        SetupChannels();
        ParseInstruments(songData, ref index);
        ReadSongData(songData, ref index);
        index += 4; // skip over the identifier, File format tag (M:K:, FLT4, FLT8, ...)
        ParsePatterns(songData, ref index);
        LoadInstruments(songData, index);

        return _song;
    }

    protected void SetupChannels()
    {
        _song.Channels = new ChannelData[_song.NumberOfTracks];
        for (var i = 0; i < _song.NumberOfTracks; i++)
        {
            _song.Channels[i] = new ChannelData();
        }
    }
    
    protected virtual void SetupBasicProperties()
    {
        _song.SourceFormat = "Protracker";
        _song.NumberOfTracks = 4;
        _song.RowsPerPattern = 64;
        _song.InstrumentsCount = 31;
        _song.OrdersCount = 128;
    }

    protected void ParseInstruments(Span<byte> songData, ref int index)
    {
        // Read in all the instrument headers - mod files have usually 31, instrument #0 is ignored !
        _song.Instruments = new Instrument[_song.InstrumentsCount];
        for (var i = 1; i < _song.InstrumentsCount; i++)
        {
            // Read the instrument name
            _song.Instruments[i] = new Instrument();
            _song.Instruments[i].Name = Encoding.ASCII.GetString(songData.Slice(index, 22));
            index += 22;
    
            // Read remaining info about instrument
            _song.Instruments[i].Length = ReadAmigaWord(songData, ref index);
            _song.Instruments[i].FineTune = songData[index++];
            if (_song.Instruments[i].FineTune > 7)
            {
                _song.Instruments[i].FineTune -= 16;
            }
    
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

    protected void ReadSongData(Span<byte> modFileDataSpan, ref int index)
    {
        // Read in song data
        _song.Length = modFileDataSpan[index++];
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

    protected void ParsePatterns(Span<byte> modFileDataSpan, ref int index)
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
        var noteData = new NoteData();

        // Get the 4 bytes for this note
        int b0 = modFileDataSpan[index++];
        int b1 = modFileDataSpan[index++];
        int b2 = modFileDataSpan[index++];
        int b3 = modFileDataSpan[index++];
                    
        // Parse them
        var period = ((b0 & 0x0F) << 8) | b1;
        if (period != 0)
        {
            noteData.Period = period;
            noteData.PeriodIndex = SongConstants.PeriodsIndex[period];
        }
        else
        {
            noteData.Period = -1;
            noteData.PeriodIndex = -1;
        }
                    
        noteData.InstrumentNumber = (b0 & 0xF0) | (b2 >> 4);
        noteData.Effect = b2 & 0x0F;
        noteData.EffectParameters = b3;

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
}
