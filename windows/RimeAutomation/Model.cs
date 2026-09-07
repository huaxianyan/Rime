using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;

namespace RimeAutomation
{
    internal sealed class Operation
    {
        public readonly string Id;
        public readonly string Label;
        public readonly bool InitiallyEnabled;
        public readonly string Description;
        private Operation(string id, string label, bool enabled, string description)
        { Id = id; Label = label; InitiallyEnabled = enabled; Description = description; }

        public static readonly Operation[] All = {
            new Operation("sync", "同步", true, "处理用户词典等同步数据，跨设备传输需要另行配置同步工具。"),
            new Operation("deploy", "重新部署", false, "使方案、配置和词库改动生效，执行期间可能短暂影响输入。")
        };
        public static Operation Find(string id)
        { return Array.Find(All, operation => operation.Id == id); }
    }

    internal sealed class Schedule
    {
        public bool Exists;
        public bool Enabled;
        public bool Logon = true;
        public bool Daily;
        public TimeSpan Time = new TimeSpan(20, 0, 0);
        public string Status = "尚未创建计划";
        public static Schedule Initial(Operation operation)
        { return new Schedule { Enabled = operation.InitiallyEnabled }; }
        public void Validate()
        {
            if (Enabled && !Logon && !Daily)
                throw new UserError("请为已开启的功能至少选择一种执行时机。");
        }
    }

    internal sealed class UserError : Exception
    {
        public UserError(string message) : base(message) { }
    }

    internal static class Results
    {
        public const int Success = 0, Failed = 1, Missing = 2, Busy = 3, InvalidArguments = 4;
        public static string Text(int code)
        {
            switch (code)
            {
                case Success: return "命令已成功完成";
                case Missing: return "暂时找不到小狼毫程序。若正在升级，请完成后重试；否则请检查安装。";
                case Busy: return "另一项操作仍在执行，本次未完成。请稍后重试。";
                case 0x41301: return "正在执行，请稍后刷新";
                case 0x41303: return "尚未执行";
                default: return "命令未成功完成。请确认小狼毫可正常使用，并在其他同步或部署结束后重试。";
            }
        }
    }

    internal sealed class AppPaths
    {
        public const string DisplayName = "小狼毫自动任务";
        public readonly string Directory;
        public readonly string Identity;
        public readonly string Shortcut;
        public string Executable { get { return Path.Combine(Directory, "RimeAutomation.exe"); } }
        public string TaskPrefix { get { return "RimeAutomation-" + Identity + "-"; } }
        public string ExecutionMutex { get { return @"Local\RimeAutomation-" + Identity; } }
        public string WindowMutex { get { return ExecutionMutex + "-Settings"; } }
        public static string UserSid { get { return WindowsIdentity.GetCurrent().User.Value; } }
        public static string CurrentExecutable { get { return Assembly.GetEntryAssembly().Location; } }
        public AppPaths(string directory, string identity, string startMenuDirectory)
        {
            Directory = directory; Identity = identity;
            Shortcut = Path.Combine(startMenuDirectory, DisplayName + ".lnk");
        }
        public static AppPaths Current()
        {
            return new AppPaths(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RimeAutomation"), UserSid, Environment.GetFolderPath(Environment.SpecialFolder.Programs));
        }
        public bool IsInstalledExecutable(string source)
        { return string.Equals(Path.GetFullPath(source), Path.GetFullPath(Executable), StringComparison.OrdinalIgnoreCase); }
    }
}
