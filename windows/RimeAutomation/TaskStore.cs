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
            var result = new Schedule { Exists = true, Enabled = task.Enabled };
            bool hasPreviousTrigger = false;
            foreach (dynamic trigger in task.Definition.Triggers)
            {
                switch ((int)trigger.Type)
                {
                    case 9:
                    case 11: hasPreviousTrigger = true; break;
                    case 2:
                        result.Time = DateTime.Parse((string)trigger.StartBoundary, CultureInfo.InvariantCulture).TimeOfDay;
                        break;
                }
            }
            result.Status = Results.Text((int)task.LastTaskResult);
            DateTime lastRun = task.LastRunTime;
            if (lastRun.Year > 2000) result.Status = lastRun.ToString("yyyy-MM-dd HH:mm") + "  " + result.Status;
            if (hasPreviousTrigger)
                result.Status = "原计划包含登录或锁屏时执行。请确认每日执行时间并保存，更新原计划。";
            return result;
        }

        public void Save(Operation operation, Schedule settings)
        {
            dynamic existing = Find(operation);
            if (!settings.Enabled && existing == null) return;
            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Source = Owner;
            definition.RegistrationInfo.Description = "小狼毫自动" + operation.Label + "，由 Rime 自动任务设置管理。";
            definition.Principal.UserId = AppPaths.UserSid;
            definition.Principal.LogonType = 3; // 锁屏仍属于已登录会话，到计划时间照常执行。
            definition.Principal.RunLevel = 0;
            definition.Settings.Enabled = settings.Enabled;
            definition.Settings.MultipleInstances = 2; // 同一计划仍在执行时不重复启动。
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.ExecutionTimeLimit = "PT0S"; // 不强行打断词典写入。
            dynamic daily = definition.Triggers.Create(2);
            daily.DaysInterval = 1;
            daily.StartBoundary = DateTime.Today.Add(settings.Time).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
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
