using System;
using System.Threading;
using System.Windows.Forms;

namespace RimeAutomation
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppPaths paths = AppPaths.Current();
            if (args.Length != 0)
                return CommandLine.Execute(args, new OperationRunner(paths, WeaselInstallation.FindExecutable));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var window = new Mutex(false, paths.WindowMutex))
            {
                bool acquired;
                try { acquired = window.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired)
                {
                    MessageBox.Show("设置窗口已经打开。请先关闭原窗口，再打开此程序。", "小狼毫自动任务");
                    return Results.Busy;
                }
                try
                {
                    using (var store = new TaskStore(paths))
                    using (var form = new SettingsForm(paths, store, AppPaths.CurrentExecutable))
                        Application.Run(form);
                    return Results.Success;
                }
                catch (Exception error)
                {
                    MessageBox.Show(error is UserError ? error.Message :
                        "无法打开自动任务设置。请确认 Windows 任务计划程序服务正常运行，然后重试。", "小狼毫自动任务");
                    return Results.Failed;
                }
                finally { window.ReleaseMutex(); }
            }
        }
    }
}
