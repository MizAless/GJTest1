// Compatibility entry point for existing batch-mode validation commands.
// The current run checks live in the project's Editor assembly.
public static class RunPrototypeValidation
{
    public static void Run()
    {
        FiveWaveRunValidation.Run();
    }
}
