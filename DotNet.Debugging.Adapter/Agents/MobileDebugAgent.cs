using DotNet.Debugging.Adapter.Extensions;
using DotNet.Debugging.Common;
using DotNet.Debugging.Common.Apple;
using DotNet.Debugging.Common.Interop;
using DotNet.Debugging.Engine;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace DotNet.Debugging.Adapter;

public class MobileDebugAgent : BaseDebugAgent<LaunchConfiguration> {
    public MobileDebugAgent(LaunchConfiguration configuration, DebugSession debugSession) : base(configuration, debugSession) { }

    public override void Connect(ManagedDebugger debugger) {
        ArgumentNullException.ThrowIfNull(Configuration.MobileOptions);

        if (Configuration.MobileOptions.Port <= 0)
            Configuration.MobileOptions.Port = RuntimeInfo.GetFreePort();

        debugger.AttachRemote(Configuration.MobileOptions.ToRemoteAttachInfo(), Configuration.JustMyCode, onListenerReady: PrepareTarget);
    }

    private void PrepareTarget() {
        ArgumentNullException.ThrowIfNull(Configuration.MobileOptions);
        Logger.Debug($"Debugger listening on {Configuration.MobileOptions.Address}:{Configuration.MobileOptions.Port}");

        var environment = new Dictionary<string, string> {
            ["CORECLR_ENABLE_PROFILING"] = "1",
            ["CORECLR_PROFILER"] = "{9DC623E8-C88F-4FD5-AD99-77E67E1D9631}",
            ["CORECLR_PROFILER_PATH"] = Path.Combine(Configuration.Program, "Contents/MonoBundle/libvsdbgremotecoreclrtarget.dylib"),
            ["CORECLR_REMOTE_DEBUGGER_IP"] = Configuration.MobileOptions.Address!,
            ["CORECLR_REMOTE_DEBUGGER_PORT"] = Configuration.MobileOptions.Port.ToString(),

            ["CORECLR_REMOTE_DEBUGGER_ISSERVER"] = "0",
            ["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug"
        };

        var open = AppleSdkLocator.OpenTool();
        var builder = new ProcessArgumentBuilder()
            .Append("-n")
            .Append("-W");
        foreach (var (key, value) in environment)
            builder.Append("--env").AppendQuoted($"{key}={value}");
        builder.AppendQuoted(Configuration.Program);

        var debuggeeProcess = new ProcessRunner(open, builder, null).Start();
        try {
            debuggeeProcess.EnableRaisingEvents = true;
            debuggeeProcess.Exited += (_, _) => DebugSession.Protocol.TrySendEvent(new TerminatedEvent());
        }
        catch (Exception ex) {
            Logger.Error($"Failed to watch the debuggee process: {ex.Message}");
        }

        Disposables.Add(() => {
            try {
                if (debuggeeProcess is { HasExited: false }) debuggeeProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex) {
                Logger.Error($"Failed to kill the debuggee process: {ex.Message}");
            }
        });
    }
}
