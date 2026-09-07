using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace RimeAutomation
{
    internal sealed class TaskStore : IDisposable
    {
        private const string Owner = "huaxianyan/Rime/WindowsAutomation/v1";
        private readonly AppPaths paths;
        private readonly dynamic service;
        private readonly dynamic folder;
        public TaskStore(AppPaths paths)
        {
            this.paths = paths;
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
            service.Connect();
            folder = service.GetFolder(@"\");
        }
        private string Name(Operation operation) { return paths.TaskPrefix + operation.Id; }
        private dynamic Find(Operation operation)
        {
            dynamic task;
            try { task = folder.GetTask(Name(operation)); }
            catch (Exception error) when (error.HResult == unchecked((int)0x80070002))
            { return null; }
            string user = task.Definition.Principal.UserId;
            // Task Scheduler 读取时可能把保存的 SID 规范化为账户名称。
            string sid = user.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase) ? user :
                new NTAccount(user).Translate(typeof(SecurityIdentifier)).Value;
            if ((string)task.Definition.RegistrationInfo.Source != Owner || sid != AppPaths.UserSid)
                throw new UserError("存在同名的其他计划。请先在 Windows 任务计划程序中检查并重命名，再重试。");
            return task;
        }

        public Schedule Read(Operation operation)
        {
            dynamic task = Find(operation);
            if (task == null) return Schedule.Initial(operation);
            var result = new Schedule { Exists = true, Enabled = task.Enabled, Logon = false };
            foreach (dynamic trigger in task.Definition.Triggers)
            {
                switch ((int)trigger.Type)
                {
                    case 9: result.Logon = true; break;
                    case 11: result.Lock = (int)trigger.StateChange == 7; break;
                    case 2:
                        result.Daily = true;
                        result.Time = DateTime.Parse((string)trigger.StartBoundary, CultureInfo.InvariantCulture).TimeOfDay;
                        break;
                }
            }
            result.Status = Results.Text((int)task.LastTaskResult);
            DateTime lastRun = task.LastRunTime;
            if (lastRun.Year > 2000) result.Status = lastRun.ToString("yyyy-MM-dd HH:mm") + "  " + result.Status;
            return result;
        }

        public void Save(Operation operation, Schedule settings)
        {
            settings.Validate();
            dynamic existing = Find(operation);
            if (!settings.Enabled && existing == null) return;
            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Source = Owner;
            definition.RegistrationInfo.Description = "小狼毫自动" + operation.Label + "，由 Rime 自动任务设置管理。";
            definition.Principal.UserId = AppPaths.UserSid;
            definition.Principal.LogonType = 3; // 当前用户登录会话，不保存密码。
            definition.Principal.RunLevel = 0;
            definition.Settings.Enabled = settings.Enabled;
            definition.Settings.MultipleInstances = 2; // 同一计划仍在执行时不重复启动。
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.ExecutionTimeLimit = "PT0S"; // 不强行打断词典写入。
            if (settings.Logon)
            {
                dynamic trigger = definition.Triggers.Create(9);
                trigger.UserId = AppPaths.UserSid;
                trigger.Delay = "PT30S";
            }
            if (settings.Lock)
            {
                dynamic trigger = definition.Triggers.Create(11);
                trigger.UserId = AppPaths.UserSid;
                trigger.StateChange = 7;
            }
            if (settings.Daily)
            {
                dynamic trigger = definition.Triggers.Create(2);
                trigger.DaysInterval = 1;
                trigger.StartBoundary = DateTime.Today.Add(settings.Time).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            }
            dynamic action = definition.Actions.Create(0);
            action.Path = paths.Executable;
            action.Arguments = CommandLine.Arguments(operation);
            action.WorkingDirectory = paths.Directory;
            folder.RegisterTaskDefinition(Name(operation), definition, 6, AppPaths.UserSid, null, 3);
        }

        public void Remove(Operation operation)
        {
            if (Find(operation) != null) folder.DeleteTask(Name(operation), 0);
        }
        public void Dispose()
        {
            Marshal.FinalReleaseComObject(folder);
            Marshal.FinalReleaseComObject(service);
        }
    }
}
