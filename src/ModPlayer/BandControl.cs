namespace ModPlayer;

public struct BandControl
{
    public ConsoleKey UpKey { get; }
    public ConsoleKey DownKey { get; }
    public ConsoleKey ResetKey { get; }

    public BandControl(ConsoleKey upKey, ConsoleKey downKey, ConsoleKey resetKey)
    {
        UpKey = upKey;
        DownKey = downKey;
        ResetKey = resetKey;
    }
}
