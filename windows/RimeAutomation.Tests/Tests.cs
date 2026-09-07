using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RimeAutomation;

internal static class Tests
{
    [STAThread]
    private static int Main(string[] args)
    {
        string here = AppDomain.CurrentDomain.BaseDirectory;
        // 此测试宿主兼作外部程序 fixture。发布包只包含生产项目的 EXE。
        if (args.Length == 1 && (args[0] == "/sync" || args[0] == "/deploy"))
        {
            File.AppendAllText(Path.Combine(Directory.GetParent(here.TrimEnd('\\')).FullName, "runs.txt"),
                new DirectoryInfo(here).Name + " " + args[0] + Environment.NewLine);
            return File.Exists(Path.Combine(here, "fail")) ? 7 : 0;
        }
        if (args.Length > 0 && args[0] == "--run")
        {
            var context = new AppPaths(here, File.ReadAllText(Path.Combine(here, "identity.txt")));
            return CommandLine.Execute(args, new OperationRunner(context,
                () => File.ReadAllText(Path.Combine(here, "installation.txt"))));
        }
        string id = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "Rime binary 测试 " + id);
        var paths = new AppPaths(Path.Combine(root, "installed"), "Test-" + id);
        TaskStore store = null;
        dynamic service = null;
        int outcome = 0;
        try
        {
            Directory.CreateDirectory(root);
            new Installer(paths).Install(AppPaths.CurrentExecutable);
            File.WriteAllText(Path.Combine(paths.Directory, "identity.txt"), "Test-" + id);
            string old = MakeFixture(root, "old");
            string current = MakeFixture(root, "new");
            string pointer = Path.Combine(paths.Directory, "installation.txt");
            File.WriteAllText(pointer, old);
            store = new TaskStore(paths);
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            service.Connect();
            foreach (string operationId in new[] { "sync", "deploy" })
            {
                var operation = Operation.Find(operationId);
                string taskName = "RimeAutomation-Test-" + id + "-" + operationId;
                var settings = new Schedule { Enabled = true, Logon = true, Lock = true, Daily = true, Time = new TimeSpan(23, 59, 0) };
                store.Save(operation, settings);
                Schedule loaded = store.Read(operation);
                Equal(loaded.Enabled && loaded.Logon && loaded.Lock && loaded.Daily, true, "保存后显示选择的执行时机");
                Equal(loaded.Time, new TimeSpan(23, 59, 0), "保存后显示选择的定时时间");
                dynamic task = service.GetFolder(@"\").GetTask(taskName);
                Equal((string)task.Definition.Actions[1].Path, Path.Combine(root, "installed", "RimeAutomation.exe"), "计划指向安装后的程序");
                Equal((string)task.Definition.Actions[1].Arguments, "--run " + operationId, "计划参数使用公开的执行入口");
                Equal((int)task.Definition.Principal.LogonType, 3, "计划使用当前用户登录会话");
                RunTask(task, 0);
                Equal(File.ReadAllLines(Path.Combine(root, "runs.txt")).Contains("old /" + operationId), true, "计划执行当前安装版本");
                File.WriteAllText(pointer, current);
                RunTask(task, 0);
                Equal(File.ReadAllLines(Path.Combine(root, "runs.txt")).Contains("new /" + operationId), true, "保留旧目录且不重建任务，升级后执行新版本");
                settings.Enabled = false;
                settings.Daily = false;
                settings.Lock = false;
                settings.Time = new TimeSpan(19, 10, 0);
                store.Save(operation, settings);
                loaded = store.Read(operation);
                Equal(loaded.Enabled, false, "用户关闭自动功能后计划停用");
                Equal(loaded.Logon && !loaded.Lock && !loaded.Daily, true, "修改后的执行时机可恢复");
                settings.Enabled = true;
                store.Save(operation, settings);
                Equal(store.Read(operation).Enabled, true, "用户重新开启后计划生效");
                File.WriteAllText(Path.Combine(root, "new", "fail"), "");
                RunTask(task, 1);
                File.Delete(Path.Combine(root, "new", "fail"));
                File.WriteAllText(pointer, old);
                Console.WriteLine("PASS：自动" + operation.Label + "创建、执行、升级、修改、停用、重新启用与失败反馈");
            }
            // 更新已安装的工具不改变现有任务的触发设置。
            new Installer(paths).Install(AppPaths.CurrentExecutable);
            Equal(store.Read(Operation.Find("sync")).Logon, true, "更新程序后保留任务设置");
            Equal(Directory.GetFiles(paths.Directory, "update-*.exe").Length, 0, "更新完成后临时程序已清理");
            VerifyInstallerReplacement(root);
            VerifyManualWindow(paths, store);
            foreach (Operation operation in Operation.All) store.Remove(operation);
            foreach (Operation operation in Operation.All) Equal(store.Read(operation).Exists, false, "用户移除后计划消失");
            Console.WriteLine("PASS：安装、更新、设置窗口手动执行与移除计划");
        }
        catch (Exception error) { Console.Error.WriteLine(error); outcome = 1; }
        finally
        {
            bool clean = true;
            if (service != null)
            {
                foreach (string operation in new[] { "sync", "deploy" })
                {
                    string name = "RimeAutomation-Test-" + id + "-" + operation;
                    try
                    {
                        dynamic task = service.GetFolder(@"\").GetTask(name);
                        task.Stop(0);
                        service.GetFolder(@"\").DeleteTask(name, 0);
                    }
                    catch (Exception error)
                    {
                        if (error.HResult != unchecked((int)0x80070002)) { Console.Error.WriteLine("临时任务清理失败：" + name); clean = false; }
                    }
                }
                Marshal.FinalReleaseComObject(service);
            }
            if (store != null) store.Dispose();
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (Exception error) { Console.Error.WriteLine("临时文件清理失败：" + error.Message); clean = false; }
            if (!clean) outcome = 1;
        }
        return outcome;
    }

    private static string MakeFixture(string root, string version)
    {
        string directory = Path.Combine(root, version);
        Directory.CreateDirectory(directory);
        string executable = Path.Combine(directory, "WeaselDeployer.exe");
        File.Copy(AppPaths.CurrentExecutable, executable);
        return executable;
    }
    private static void RunTask(dynamic task, int expectedResult)
    {
        dynamic instance = task.Run(null);
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            Thread.Sleep(100);
            try { instance.Refresh(); }
            catch (Exception error) when (error.HResult == unchecked((int)0x8004130B))
            { break; }
            if (DateTime.UtcNow > deadline) throw new Exception("临时任务未在 30 秒内完成");
        } while ((int)instance.State == 2 || (int)instance.State == 4);
        Equal((int)task.LastTaskResult, expectedResult, "任务反馈实际执行结果");
    }
    private static void VerifyInstallerReplacement(string root)
    {
        var target = new AppPaths(Path.Combine(root, "replacement"), "Replace-" + Guid.NewGuid().ToString("N"));
        string input = Path.Combine(root, "input.exe");
        File.WriteAllText(input, "first release");
        new Installer(target).Install(input);
        Equal(File.ReadAllText(target.Executable), "first release", "首次安装复制所选文件");
        File.WriteAllText(input, "second release");
        new Installer(target).Install(input);
        Equal(File.ReadAllText(target.Executable), "second release", "更新替换旧文件");
    }
    private static void VerifyManualWindow(AppPaths paths, TaskStore store)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = true;
        foreach (string operation in new[] { "sync", "deploy" })
        {
            using (var form = new SettingsForm(paths, store, paths.Executable))
            using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
            {
                bool started = false, completed = false;
                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                timer.Tick += (sender, args) => {
                    if (!started)
                    {
                        started = true;
                        ((Button)form.Controls.Find("Run-" + operation, true)[0]).PerformClick();
                    }
                    string status = form.Controls.Find("Status-" + operation, true)[0].Text;
                    if (status == "本次手动执行：命令已成功完成") { completed = true; form.Close(); }
                    else if (DateTime.UtcNow > deadline) form.Close();
                };
                timer.Start();
                form.ShowDialog();
                Equal(completed, true, "用户点击立即执行后窗口显示成功结果：" + operation);
            }
        }
    }
    private static void Equal<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected)) throw new Exception(message + "\nExpected: " + expected + "\nActual: " + actual);
    }
}
