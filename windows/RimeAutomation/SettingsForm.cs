using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RimeAutomation
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppPaths paths;
        private readonly TaskStore store;
        private readonly string source;
        private readonly Dictionary<Operation, ScheduleControls> rows = new Dictionary<Operation, ScheduleControls>();
        private readonly Button save = new Button();
        private readonly Button remove = new Button();
        private bool running;

        public SettingsForm(AppPaths paths, TaskStore store, string source)
        {
            this.paths = paths; this.store = store; this.source = source;
            Text = "小狼毫自动任务";
            Font = new Font("Microsoft YaHei UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(680, 640);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Add(new Label { Text = "同步与重新部署分别设置，保存后生效。仅在当前用户登录期间运行。" }, 18, 14, 644, 32);
            int top = 52;
            foreach (Operation operation in Operation.All)
            {
                var row = new ScheduleControls(operation);
                row.Group.SetBounds(18, top, 644, 210);
                Controls.Add(row.Group);
                rows.Add(operation, row);
                row.Run.Click += async (sender, args) => await RunNow(operation);
                top += 218;
            }
            Add(new Label {
                Text = "同步位置请在 Rime 用户文件夹的 installation.yaml 中确认。使用网盘同步配置时，请在文件下载完成后重新部署。"
            }, 18, 494, 644, 48);
            var location = new LinkLabel { Text = "安装位置：" + paths.Directory, AutoEllipsis = true };
            location.LinkClicked += (sender, args) => OpenInstallDirectory();
            Add(location, 18, 546, 644, 28);
            remove.Text = "移除所有计划";
            remove.Click += (sender, args) => RemoveSchedules();
            Add(remove, 18, 591, 138, 32);
            var refresh = new Button { Text = "刷新状态" };
            refresh.Click += (sender, args) => RefreshStatus();
            Add(refresh, 170, 591, 110, 32);
            var close = new Button { Text = "关闭" };
            close.Click += (sender, args) => Close();
            Add(close, 392, 591, 100, 32);
            save.Text = paths.IsInstalledExecutable(source) ? "保存设置" :
                File.Exists(paths.Executable) ? "更新程序并保存" : "安装并保存";
            save.Click += (sender, args) => SaveSchedules();
            Add(save, 506, 591, 156, 32);
            LoadSchedules();
        }

        private void Add(Control control, int x, int y, int width, int height)
        { control.SetBounds(x, y, width, height); Controls.Add(control); }
        private void Tell(string message)
        { MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        private void Report(Exception error, string fallback)
        { Tell(error is UserError ? error.Message : fallback); }
        private void LoadSchedules()
        {
            foreach (var item in rows) item.Value.Load(store.Read(item.Key));
        }
        private void RefreshStatus()
        {
            if (running) return;
            try { foreach (var item in rows) item.Value.Status.Text = store.Read(item.Key).Status; }
            catch (Exception error) { Report(error, "无法读取执行状态。请检查 Windows 任务计划程序是否正常运行，然后重试。"); }
        }
        private void SaveSchedules()
        {
            var pending = new Dictionary<Operation, Schedule>();
            try
            {
                foreach (var item in rows)
                {
                    Schedule schedule = item.Value.Read();
                    schedule.Validate();
                    // 在安装及写入前检查全部计划的归属。
                    store.Read(item.Key);
                    pending.Add(item.Key, schedule);
                }
                if (!paths.IsInstalledExecutable(source) && MessageBox.Show(this,
                    "程序将安装或更新到：\n" + paths.Directory + "\n\n当前选择的计划也会一并保存，是否继续？",
                    Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                new Installer(paths).Install(source);
                foreach (var item in pending) store.Save(item.Key, item.Value);
                LoadSchedules();
                Tell("设置已保存。关闭此窗口后，Windows 仍会按计划执行。\n以后可以从安装目录打开程序，或下载新版程序进行更新。");
            }
            catch (Exception error)
            {
                Report(error, "未能保存全部设置。请重新打开窗口确认已保存的项目，并检查 Windows 任务计划程序是否正常运行，然后重试。");
            }
        }
        private void RemoveSchedules()
        {
            if (MessageBox.Show(this, "移除本工具创建的同步和重新部署计划？\n词库和配置保留在原位置，已开始的操作会继续完成。",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                foreach (Operation operation in Operation.All) store.Remove(operation);
                LoadSchedules();
                Tell("计划已移除。词库和配置保留在原位置。\n如需卸载工具，请打开安装位置，关闭本窗口后删除 RimeAutomation.exe。");
            }
            catch (Exception error) { Report(error, "未能移除全部计划。请在 Windows 任务计划程序中检查后重试。"); }
        }
        private void OpenInstallDirectory()
        {
            if (!Directory.Exists(paths.Directory)) { Tell("程序尚未安装。点击「安装并保存」后，可以在这里打开安装目录。"); return; }
            try { Process.Start(new ProcessStartInfo(paths.Directory) { UseShellExecute = true }); }
            catch { Tell("无法打开安装目录。请在文件资源管理器中打开以下位置：\n" + paths.Directory); }
        }
        private async Task RunNow(Operation operation)
        {
            running = true;
            foreach (var row in rows.Values) row.Run.Enabled = false;
            save.Enabled = remove.Enabled = false;
            rows[operation].Status.Text = "正在执行，请稍候……";
            int result;
            try
            {
                // 单独启动进程，使关闭设置窗口不会中断词典写入。
                result = await Task.Run(() => {
                    using (Process process = Process.Start(new ProcessStartInfo(source, CommandLine.Arguments(operation)) {
                        UseShellExecute = false, CreateNoWindow = true
                    }))
                    {
                        process.WaitForExit();
                        return process.ExitCode;
                    }
                });
            }
            catch { result = Results.Failed; }
            if (IsDisposed) return;
            rows[operation].Status.Text = "本次手动执行：" + Results.Text(result);
            foreach (var row in rows.Values) row.Run.Enabled = true;
            save.Enabled = remove.Enabled = true;
            running = false;
        }

        private sealed class ScheduleControls
        {
            public readonly GroupBox Group = new GroupBox();
            public readonly CheckBox Enabled = new CheckBox { Text = "启用" };
            public readonly CheckBox Logon = new CheckBox { Text = "登录 Windows 后" };
            public readonly CheckBox Lock = new CheckBox { Text = "锁定电脑时" };
            public readonly CheckBox Daily = new CheckBox { Text = "每天定时" };
            public readonly DateTimePicker Time = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };
            public readonly Label Status = new Label();
            public readonly Button Run = new Button();
            public ScheduleControls(Operation operation)
            {
                Group.Text = "自动" + operation.Label;
                Put(Enabled, 545, 22, 80, 28);
                Put(new Label { Text = operation.Description }, 14, 28, 520, 26);
                Put(Logon, 14, 68, 185, 28);
                Put(Lock, 215, 68, 155, 28);
                Put(Daily, 390, 68, 105, 28);
                Put(Time, 507, 68, 117, 28);
                Status.Name = "Status-" + operation.Id;
                Put(Status, 14, 109, 610, 48);
                Run.Text = "立即" + operation.Label;
                Run.Name = "Run-" + operation.Id;
                Put(Run, 14, 165, 140, 30);
            }
            private void Put(Control control, int x, int y, int width, int height)
            { control.SetBounds(x, y, width, height); Group.Controls.Add(control); }
            public Schedule Read()
            { return new Schedule { Enabled = Enabled.Checked, Logon = Logon.Checked, Lock = Lock.Checked, Daily = Daily.Checked, Time = Time.Value.TimeOfDay }; }
            public void Load(Schedule settings)
            {
                Enabled.Checked = settings.Enabled; Logon.Checked = settings.Logon;
                Lock.Checked = settings.Lock; Daily.Checked = settings.Daily;
                Time.Value = DateTime.Today.Add(settings.Time); Status.Text = settings.Status;
            }
        }
    }
}
