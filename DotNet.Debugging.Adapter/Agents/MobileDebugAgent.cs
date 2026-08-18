using DotNet.Debugging.Common;
using DotNet.Debugging.Common.Apple;
using DotNet.Debugging.Common.Extensions;
using DotNet.Debugging.Common.Interop;
using DotNet.Debugging.Engine;
using DotNet.Debugging.Engine.Models;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace DotNet.Debugging.Adapter;

public class MobileDebugAgent : BaseDebugAgent<LaunchConfiguration> {
    public MobileDebugAgent(LaunchConfiguration configuration, DebugSession debugSession) : base(configuration, debugSession) { }

    public override void Connect(ManagedDebugger debugger) {
        ArgumentNullException.ThrowIfNull(Configuration.MobileOptions);
        debugger.AttachRemote(GetAttachInfo(), Configuration.JustMyCode, onListenerReady: () => {
            Logger.Debug($"Debugger listening on {Configuration.MobileOptions.Address}:{Configuration.MobileOptions.Port}");

            var profilerPath = CopyProfilerLibraryToAssembliesPath();
            Configuration.EnvironmentVariables.Add("CORECLR_ENABLE_PROFILING", "1");
            Configuration.EnvironmentVariables.Add("CORECLR_PROFILER", "{9DC623E8-C88F-4FD5-AD99-77E67E1D9631}");
            Configuration.EnvironmentVariables.Add("CORECLR_PROFILER_PATH", profilerPath);
            Configuration.EnvironmentVariables.Add("CORECLR_REMOTE_DEBUGGER_IP", Configuration.MobileOptions.Address!);
            Configuration.EnvironmentVariables.Add("CORECLR_REMOTE_DEBUGGER_PORT", Configuration.MobileOptions.Port.ToString());
            Configuration.EnvironmentVariables.Add("CORECLR_REMOTE_DEBUGGER_ISSERVER", Configuration.MobileOptions.IsServer ? "0" : "1");
            Configuration.EnvironmentVariables.Add("DOTNET_MODIFIABLE_ASSEMBLIES", "debug");

            switch (Configuration.Runtime) {
                case CoreRuntime.Android:
                    // LaunchAndroid();
                    break;
                case CoreRuntime.IOS:
                    // LaunchAppleMobile();
                    break;
                case CoreRuntime.Maccatalyst:
                    LaunchMacCatalyst();
                    break;
                case CoreRuntime.CoreClr:
                    throw new NotSupportedException();
            }
        });
    }

    private void LaunchMacCatalyst() {
        var open = AppleSdkLocator.OpenTool();
        var builder = new ProcessArgumentBuilder()
            .Append("-n", "-W");
        foreach (var kvp in Configuration.EnvironmentVariables)
            builder.Append("--env").AppendQuoted($"{kvp.Key}={kvp.Value}");
        builder.AppendQuoted(Configuration.Program);

        var debuggeeProcess = new ProcessRunner(open, builder, ProcessLogger).Start();
        debuggeeProcess.AddFinalizer(() => Protocol.SendEvent(new TerminatedEvent()));

        Disposables.Add(() => SafeExtensions.Invoke(() => debuggeeProcess.Terminate(entireProcessTree: true)));
    }

    private string GetCoreclrTargetLibrary() {
        ArgumentNullException.ThrowIfNull(Configuration.MobileOptions?.Platform);
        var parts = Configuration.MobileOptions.Platform.Split(';');
        if (parts.Length != 2)
            throw new ArgumentException(Resources.MsgMissingPlatform);

        var libraryName = "libvsdbgremotecoreclrtarget" + (Configuration.Runtime == CoreRuntime.Android ? ".so" : ".dylib");
        var libraryPath = Path.Combine(Configuration.RemoteTargetDirectory!, parts[0], $"{parts[0]}-{parts[1]}", libraryName);
        if (!File.Exists(libraryPath))
            throw new FileNotFoundException(libraryPath);

        return libraryPath;
    }
    private string GetCoreclrHostLibrary() {
        var runtime = $"{RuntimeInfo.GetOperationSystem()}-{RuntimeInfo.GetArchitecture()}";
        var libraryName = "libremotemscordbihost" + (Configuration.Runtime == CoreRuntime.Android ? ".so" : ".dylib");
        var libraryPath = Path.Combine(Configuration.RemoteHostDirectory!, runtime, libraryName);
        if (!File.Exists(libraryPath))
            throw new FileNotFoundException(libraryPath);

        return libraryPath;
    }
    private string CopyProfilerLibraryToAssembliesPath() {
        ArgumentNullException.ThrowIfNullOrEmpty(Configuration.MobileOptions?.AssetsPath);
        var profilerSourcePath = GetCoreclrTargetLibrary();
        var profilerName = Path.GetFileName(profilerSourcePath);
        var profilerDestPath = Path.Combine(Configuration.MobileOptions.AssetsPath, profilerName);
        File.Copy(profilerSourcePath, profilerDestPath, true);
        return profilerDestPath;
    }
    private RemoteAttachInfo GetAttachInfo() {
        ArgumentNullException.ThrowIfNull(Configuration.MobileOptions);

        if (Configuration.MobileOptions.Port <= 0)
            Configuration.MobileOptions.Port = RuntimeInfo.GetFreePort();
        if (Configuration.MobileOptions.Address == null)
            Configuration.MobileOptions.Address = "127.0.0.1";

        var mscordbiPath = GetCoreclrHostLibrary();
        return new RemoteAttachInfo {
            Platform = Configuration.MobileOptions.Platform ?? string.Empty,
            Address = Configuration.MobileOptions.Address,
            Port = Configuration.MobileOptions.Port,
            IsServer = Configuration.MobileOptions.IsServer,
            AssembliesPath = $"{Configuration.MobileOptions.AssetsPath};{Path.GetDirectoryName(mscordbiPath)}",
            MscordbiPath = mscordbiPath,
        };
    }
}
