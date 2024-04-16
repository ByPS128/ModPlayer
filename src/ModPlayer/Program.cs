using ModPlayer.SongLoaders;
using NAudio.Wave;

namespace ModPlayer;

public sealed class Program
{
    //private static string modFileNameToPlay = "Mods\\space_debris.mod";
    //private static string modFileNameToPlay = "Mods\\klisje.mod";
    //private static string modFileNameToPlay = "Mods\\lotus2-title.mod";
    //private static string modFileNameToPlay = "Mods\\sahara.mod";
    //private static string modFileNameToPlay = "Mods\\testlast.mod";
    //private static string modFileNameToPlay = "Mods\\brimble-superfrog-title.mod";
    //private static readonly string modFileNameToPlay = "Mods\\lethald2.mod";
    //private static string modFileNameToPlay = "Mods\\desert1.mod";
    //private static string modFileNameToPlay = "Mods\\desert2.mod";
    //private static string modFileNameToPlay = "Mods\\desert3.mod";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\Vibrato\\vibrato-04.mod";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\LaxityTracker\\HiddenPart.unic";
    private static string modFileNameToPlay = "Mods\\TestFiles\\FastTracker\\8_belle-helene-8ch.md8";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\FastTracker\\pitzdahero-6ch).ft";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("\n\n\nMod Player\n");
        try
        {
            if (DetermineIfParameterPresent(args, "-writefile"))
            {
                var wavModFileName = Path.Combine("C:\\temp\\", Path.GetFileName(modFileNameToPlay) + ".wav");
                Console.WriteLine($"Temporary output audio file: {wavModFileName}");

                // create a wave file with specified time length.
                await CreateWaveFile(modFileNameToPlay, 2 * 60 * 1000, wavModFileName);
                Console.WriteLine("\n\nWave file created.\n\n");

                return;
            }

            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel(); // Indicates that cancellation has been requested
                Console.WriteLine("\nTerminating the application...");
            };

            Console.WriteLine("Mod Player started. Press CTRL+C to stop.");

            // Passing a CancellationToken to a running task/loop
            await PlayAudioInfinitely(cancellationTokenSource.Token);
        }
        finally
        {
            Console.WriteLine("Mod Player stopped.");
        }
    }

    private static Task PlayAudioInfinitely(CancellationToken cancellationToken)
    {
        var song = SongLoader.LoadFromFile(modFileNameToPlay);
        song.DescribeSong((item, value) => Console.WriteLine($"{item}: {value}"));
        
        var modPlayer = new ModPlay();
        modPlayer.PrepareToPlay(song, 44100, 16, ChannelsVariation.StereoPan, 32);
        modPlayer.SetStereoPan(50);
        SongTools.WriteInstrumentsToFiles(song);
        //modPlayer.JumpToOrder(80);
        // modPlayer.TurnOnOffAllChannels(false);
        // modPlayer.TurnOnOffChannel(1, true);
        modPlayer.Play();

        // A loop that runs until Ctrl+C is caught
        // This also makes it possible to play the generated audio.
        while (!cancellationToken.IsCancellationRequested) Thread.Sleep(100); // A short pause to make the loop cycle more CPU friendly

        return Task.CompletedTask;
    }

    private static bool DetermineIfParameterPresent(string[]? args, string parameterName)
    {
        if (args is null || args.Length == 0)
        {
            return false;
        }

        return args
            .Where(i => !string.IsNullOrWhiteSpace(i) && i.StartsWith("-"))
            .Any(i => string.Equals(i, parameterName, StringComparison.OrdinalIgnoreCase));
    }

    private static Task CreateWaveFile(string modFileName, int milliseconds, string fileName)
    {
        var song = SongLoader.LoadFromFile(modFileNameToPlay);
        var modPlayer = new ModPlay();
        modPlayer.PrepareToPlay(song, 44100, 16, ChannelsVariation.StereoPan, 64);

        using var waveOut = new WaveFileWriter(fileName, modPlayer.WaveFormat);
        modPlayer.WriteWaveData(waveOut, milliseconds);

        return Task.CompletedTask;
    }
}
