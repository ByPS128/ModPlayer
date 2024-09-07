using ModPlayer.SongLoaders;
using NAudio.Wave;

namespace ModPlayer;

public sealed class Program
{
    //private static string modFileNameToPlay = "Mods\\space_debris.mod";
    private static string modFileNameToPlay = "Mods\\klisje.mod";
    //private static readonly string modFileNameToPlay = "Mods\\lotus2-title.mod";
    //private static string modFileNameToPlay = "Mods\\sahara.mod";
    //private static string modFileNameToPlay = "Mods\\testlast.mod";
    //private static string modFileNameToPlay = "Mods\\brimble-superfrog-title.mod";
    //private static readonly string modFileNameToPlay = "Mods\\lethald2.mod";
    //private static string modFileNameToPlay = "Mods\\desert1.mod";
    //private static string modFileNameToPlay = "Mods\\desert2.mod";
    //private static string modFileNameToPlay = "Mods\\desert3.mod";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\Vibrato\\vibrato-04.mod";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\LaxityTracker\\HiddenPart.unic";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\FastTracker\\8_belle-helene-8ch.md8";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\FastTracker\\pitzdahero-6ch).ft";
    //private static string modFileNameToPlay = "Mods\\TestFiles\\LaxityTracker2\\HiddenPart.unic";

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

        var numberOfBands = 5;
        var modPlayer = new ModPlay();
        modPlayer.PrepareToPlay(song, 44100, 16, ChannelsVariation.StereoPan, 32);
        modPlayer.SetStereoPan(50);
        modPlayer.SetEqualizer(numberOfBands); // Inicializujeme ekvalizér
        modPlayer.SetEqualizerGain(0, 24.0f);
        modPlayer.SetEqualizerGain(1, 16.0f);
        modPlayer.SetEqualizerGain(2, -6.0f);
        modPlayer.SetEqualizerGain(3, 12.0f);
        modPlayer.SetEqualizerGain(4, 19.0f);
        SongTools.WriteInstrumentsToFiles(song);
        //modPlayer.JumpToOrder(80);
        // modPlayer.TurnOnOffAllChannels(false);
        // modPlayer.TurnOnOffChannel(1, true);
        modPlayer.Play();

        // // A loop that runs until Ctrl+C is caught
        // // This also makes it possible to play the generated audio.
        // while (!cancellationToken.IsCancellationRequested) Thread.Sleep(100); // A short pause to make the loop cycle more CPU friendly

        // Inicializace BandControls dynamicky podle počtu pásem
        var bandControls = InitializeBandControls(numberOfBands);
        
        // Dynamická inicializace pole pro zisky
        var gains = new float[numberOfBands]; // Dynamicky podle počtu pásem
        const float gainStep = 1.0f; // Kolik přidávat/ubírat na zisku při každém stisknutí
        const float minGain = -24.0f; // Minimální hodnota zisku
        const float maxGain = 24.0f; // Maximální hodnota zisku

        // A loop that runs until Ctrl+C is caught
        while (!cancellationToken.IsCancellationRequested)
            if (modPlayer.NumberOfBands > 0 && modPlayer.Equalizer is not null && Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Tab)
                {
                    // Přepnutí stavu ekvalizéru
                    modPlayer.Equalizer.IsActive = !modPlayer.Equalizer.IsActive;
                    Console.WriteLine($"Equalizer {(modPlayer.Equalizer.IsActive ? "enabled" : "disabled")}");
                }

                if (modPlayer.Equalizer.IsActive is false)
                {
                    // Pokud je ekvalizér zakázán, neprovádíme žádné změny
                    Thread.Sleep(100);
                    continue;
                }

                // Iterace přes pásma a kontrola kláves
                var gainChanged = false;
                for (int i = 0; i < bandControls.Length; i++)
                {
                    var control = bandControls[i];
                    if (key == control.UpKey || key == control.DownKey || key == control.ResetKey)
                    {
                        if (key == control.UpKey)
                        {
                            gains[i] = Math.Min(maxGain, gains[i] + gainStep);
                            gainChanged = true;
                        }
                        else if (key == control.DownKey)
                        {
                            gains[i] = Math.Max(minGain, gains[i] - gainStep);
                            gainChanged = true;
                        }
                        else if (key == control.ResetKey)
                        {
                            gains[i] = 0.0f; // Reset zisku
                            gainChanged = true;
                        }
                    }
                }

                // Pokud se změnil zisk, použijeme nový zisk pro dané pásmo
                if (gainChanged)
                {
                    for (var i = 0; i < gains.Length; i++)
                    {
                        modPlayer.SetEqualizerGain(i, gains[i]);
                    }

                    // Zobrazíme aktuální hodnoty zisků
                    Console.WriteLine($"Gains: [{string.Join(", ", gains)}]");
                }
            }
            else
            {
                // Pokud klávesa není stisknuta, kontrolujeme čas a provádíme krátkou pauzu
                Thread.Sleep(100);
            }

        return Task.CompletedTask;
    }

    private static BandControl[] InitializeBandControls(int numberOfBands)
    {
        var upKeys = new ConsoleKey[] { ConsoleKey.Q, ConsoleKey.W, ConsoleKey.E, ConsoleKey.R, ConsoleKey.T };
        var downKeys = new ConsoleKey[] { ConsoleKey.A, ConsoleKey.S, ConsoleKey.D, ConsoleKey.F, ConsoleKey.G };
        var resetKeys = new ConsoleKey[] { ConsoleKey.Y, ConsoleKey.X, ConsoleKey.C, ConsoleKey.V, ConsoleKey.B };

        var bandControls = new BandControl[numberOfBands];

        for (int i = 0; i < numberOfBands; i++)
        {
            bandControls[i] = new BandControl(upKeys[i], downKeys[i], resetKeys[i]);
        }

        return bandControls;
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
