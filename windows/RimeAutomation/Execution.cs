using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace RimeAutomation
{
    internal static class WeaselInstallation
    {
        public static string FindExecutable()
        {
            // 官方安装器写入 WeaselRoot，不能根据目录名称猜测当前版本。
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Rime\Weasel"))
                {
                    string directory = key == null ? null : key.GetValue("WeaselRoot") as string;
                    if (string.IsNullOrWhiteSpace(directory)) continue;
                    string executable = Path.Combine(directory, "WeaselDeployer.exe");
                    if (File.Exists(executable)) return executable;
                }
            }
            return null;
        }
    }

    internal sealed class ExecutionGate : IDisposable
    {
        private readonly Mutex mutex;
        public bool Acquired { get; private set; }
        public ExecutionGate(AppPaths paths, TimeSpan timeout)
        {
            mutex = new Mutex(false, paths.ExecutionMutex);
            try { Acquired = mutex.WaitOne(timeout); }
            catch (AbandonedMutexException) { Acquired = true; }
        }
        public void Dispose()
        {
            if (Acquired) mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    internal sealed class OperationRunner
    {
        private readonly AppPaths paths;
        private readonly Func<string> findExecutable;
        public OperationRunner(AppPaths paths, Func<string> findExecutable)
        { this.paths = paths; this.findExecutable = findExecutable; }

        public int Run(Operation operation)
        {
            try
            {
                using (var gate = new ExecutionGate(paths, TimeSpan.FromMinutes(5)))
                {
                    if (!gate.Acquired) return Results.Busy;
                    string executable = findExecutable();
                    if (executable == null || !File.Exists(executable)) return Results.Missing;
                    var start = new ProcessStartInfo(executable, "/" + operation.Id) {
                        UseShellExecute = false, CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(executable)
                    };
                    using (Process process = Process.Start(start))
                    {
                        // 小狼毫 /sync 等待维护线程结束，/deploy 在进程内完成部署。
                        process.WaitForExit();
                        return process.ExitCode == 0 ? Results.Success : Results.Failed;
                    }
                }
            }
            catch { return Results.Failed; }
        }
    }

    internal static class CommandLine
    {
        public static int Execute(string[] args, OperationRunner runner)
        {
            if (args.Length != 2 || args[0] != "--run") return Results.InvalidArguments;
            Operation operation = Operation.Find(args[1]);
            return operation == null ? Results.InvalidArguments : runner.Run(operation);
        }
        public static string Arguments(Operation operation) { return "--run " + operation.Id; }
    }
}
