using System.Text;
using ModPlayer.Models;

namespace ModPlayer;

public sealed partial class ModPlay
{
    public void LoadFromFile(string modFileName)
    {
        _modFileName = modFileName;
        var stream = File.OpenRead(modFileName);
        LoadFromStream(stream);
    }

    public void LoadFromStream(FileStream stream)
    {
        var modFileData = ReadStreamToByteArray(stream);
        LoadFromSpan(new Span<byte>(modFileData));
    }

    public void LoadFromSpan(Span<byte> modFileDataSpan)
    {
        var index = 0;
        ParseBasincProperties(modFileDataSpan, ref index);
        SetupChannels();
        ParseInstruments(modFileDataSpan, ref index);
        ReadSongData(modFileDataSpan, ref index);
        index += 4; // skip over the identifier, File format tag (M:K:, FLT4, FLT8, ...)
        ParsePatterns(modFileDataSpan, ref index);
        LoadInstruments(modFileDataSpan, index);
    }

    private void LoadInstruments(Span<byte> modFileDataSpan, int index)
    {
        // Load in the instrument data
        for (var i = 1; i < _instrumentsCount; i++)
        {
            if (_instruments[i].Length == 0)
            {
                continue;
            }
    
            var sourceSpan = modFileDataSpan.Slice(index, _instruments[i].Length);
            var instrumentRawData = new byte[_instruments[i].Length + 1]; // Allocate extra byte for anti-aliasing
            sourceSpan.CopyTo(instrumentRawData); 
            instrumentRawData[_instruments[i].Length] = instrumentRawData[_instruments[i].Length - 1];    
            _instruments[i].Data = instrumentRawData; // Assign the processed data back to the instrument

            // Correct the loop end if needed.
            if (_instruments[i].LoopEnd > _instruments[i].Length)
            {
                _instruments[i].LoopEnd = _instruments[i].Length;
            }
   
            index += _instruments[i].Length;
        }
    }

    private void ParsePatterns(Span<byte> modFileDataSpan, ref int index)
    {
        // Load in the pattern data
        _patterns = new Pattern[_patternsCount];
        for (var pattern = 0; pattern < _patternsCount; pattern++)
        {
            // Set the number of rows for this pattern, for mods it's always 64
            _patterns[pattern] = new Pattern();
            _patterns[pattern].Row = new RowData[_rowsCount];
    
            // Loop through each row
            for (var row = 0; row < _rowsCount; row++)
            {
                // Set the number of notes for this pattern
                _patterns[pattern].Row[row] = new RowData();
                _patterns[pattern].Row[row].Note = new NoteData[_numberOfTracks];

                ParseRowNotes(modFileDataSpan, _patterns[pattern].Row[row].Note, ref index);
            }
        }
    }

    private void ParseRowNotes(Span<byte> modFileDataSpan, NoteData[] notes, ref int index)
    {
        // Loop through each note
        for (var note = 0; note < _numberOfTracks; note++)
        {
            notes[note] = ParseTrackNote(modFileDataSpan, ref index);
        }
    }

    private NoteData ParseTrackNote(Span<byte> modFileDataSpan, ref int index)
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
            noteData.PeriodIndex = _periodsIndex[period];
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

    private void ReadSongData(Span<byte> modFileDataSpan, ref int index)
    {
        // Read in song data
        _songLength = modFileDataSpan[index++];
        index++; // Skip over this byte, it's no longer used
        _patternsCount = 0;
        _orders = new int[_ordersCount];
        for (var i = 0; i < _ordersCount; i++)
        {
            _orders[i] = modFileDataSpan[index++];
            _patternsCount = Math.Max(_patternsCount, _orders[i]);
        }
    
        _patternsCount++;
    }

    private void ParseInstruments(Span<byte> modFileDataSpan, ref int index)
    {
        // Read in all the instrument headers - mod files have usually 31, instrument #0 is ignored !
        _instruments = new Instrument[_instrumentsCount];
        for (var i = 1; i < _instrumentsCount; i++)
        {
            // Read the instrument name
            _instruments[i] = new Instrument();
            _instruments[i].Name = Encoding.ASCII.GetString(modFileDataSpan.Slice(index, 22));
            index += 22;
    
            // Read remaining info about instrument
            _instruments[i].Length = ReadAmigaWord(modFileDataSpan, ref index);
            _instruments[i].FineTune = modFileDataSpan[index++];
            if (_instruments[i].FineTune > 7)
            {
                _instruments[i].FineTune -= 16;
            }
    
            _instruments[i].Volume = modFileDataSpan[index++];
            _instruments[i].LoopStart = ReadAmigaWord(modFileDataSpan, ref index);
            _instruments[i].LoopLength = ReadAmigaWord(modFileDataSpan, ref index);
            _instruments[i].LoopEnd = _instruments[i].LoopStart + _instruments[i].LoopLength;
    
            // Fix loop end in case it goes too far
            if (_instruments[i].LoopEnd > _instruments[i].Length)
            {
                _instruments[i].LoopEnd = _instruments[i].Length;
            }
        }
    }

    private void SetupChannels()
    {
        _channels = new ChannelData[_numberOfTracks];
        for (var i = 0; i < _numberOfTracks; i++)
        {
            _channels[i] = new ChannelData();
        }
    }

    private void ParseBasincProperties(Span<byte> modFileDataSpan, ref int index)
    {
        // The number of instruments, channels depends on whether the sign type, like M.K., FLT8, 8CHN, etc.
        var sign = Encoding.ASCII.GetString(modFileDataSpan.Slice(1080, 4));
        if (sign is "M.K." or "FLT4" or "4CH")
        {
            _instrumentsCount = 32;
            _numberOfTracks = 4;
            _rowsCount = 64;
            _ordersCount = 128;
        }
        else if (sign is "6CHN")
        {
            _instrumentsCount = 32;
            _numberOfTracks = 6;
            _rowsCount = 64;
            _ordersCount = 128;
        }
        else if (sign is "8CHN" or "FLT8")
        {
            _instrumentsCount = 32;
            _numberOfTracks = 8;
            _rowsCount = 64;
            _ordersCount = 128;
        }
        else if (sign is "12CH")
        {
            _instrumentsCount = 32;
            _numberOfTracks = 12;
            _rowsCount = 64;
            _ordersCount = 128;
        }
        else
        {
            throw new ApplicationException("Unsupported MOD file format.");
        }

        // Get the name
        _songName = Encoding.ASCII.GetString(modFileDataSpan.Slice(index, 20));
        index += 20;

        _trackData = new TrackData[_numberOfTracks];
        _isCurrentlyPlaying = false;
    }

    private byte[] ReadStreamToByteArray(Stream stream)
    {
        var buffer = new byte[stream.Length];
        var bytesReadTotal = 0;
        var bytesToRead = buffer.Length;

        while (bytesReadTotal < bytesToRead)
        {
            var bytesRead = stream.Read(buffer, bytesReadTotal, bytesToRead - bytesReadTotal);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Could not read the stream to the end.");
            }
            bytesReadTotal += bytesRead;
        }
        return buffer;
    }
}
