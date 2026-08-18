namespace DotNet.Debugging.Adapter;

public static class Resources {
    public const string MsgMissingProcess = "Must specify either processId or processName.";
    public const string MsgNoRunningProcesses = "No process with the specified name is currently running.";
    public const string MsgMultipleProcesses = "Multiple processes were found matching the process name. Attach by process id instead.";
    public const string MsgInvalidProgram = "launch: program '{0}' does not exist.";

    public const string MsgCannotFindPdb = "Cannot find or open the PDB file.";
    public const string MsgPdbLoaded = "Symbols loaded.";
    public const string MsgPdfSkipped = "Skipped loading symbols. Module is optimized and the debugger option 'Just My Code' is enabled.";

    public const string MsgMissingPlatform = "Missing required property: 'platform'.";
    public const string MsgMissingAssets = "Missing required property: 'assets'.";
    public const string MsgMissingCoreclrHost = "coreclrHost: TODO";
    public const string MsgMissingCoreclrTarget = "coreclrTarget: TODO";
}