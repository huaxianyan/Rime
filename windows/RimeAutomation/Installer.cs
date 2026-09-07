using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RimeAutomation
{
    internal sealed class Installer
    {
        private readonly AppPaths paths;
        public Installer(AppPaths paths) { this.paths = paths; }
        public void Install(string source)
        {
            if (paths.IsInstalledExecutable(source)) { CreateShortcut(); return; }
            using (var gate = new ExecutionGate(paths, TimeSpan.Zero))
            {
                if (!gate.Acquired)
                    throw new UserError("同步或重新部署仍在执行。请完成后再次安装或更新。");
                Directory.CreateDirectory(paths.Directory);
                string staging = Path.Combine(paths.Directory, "update-" + Guid.NewGuid().ToString("N") + ".exe");
                try
                {
                    File.Copy(source, staging);
                    if (File.Exists(paths.Executable)) File.Replace(staging, paths.Executable, null);
                    else File.Move(staging, paths.Executable);
                }
                catch (IOException)
                {
                    throw new UserError("无法更新程序文件。请关闭已安装的设置窗口，等待同步或部署完成后重试。");
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UserError("无法写入安装目录。请检查该目录的访问权限或安全软件提示，然后重试。");
                }
                finally { if (File.Exists(staging)) File.Delete(staging); }
                CreateShortcut();
            }
        }

        private void CreateShortcut()
        {
            dynamic shell = null;
            dynamic shortcut = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(paths.Shortcut));
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                shortcut = shell.CreateShortcut(paths.Shortcut);
                if (File.Exists(paths.Shortcut) && !paths.IsInstalledExecutable((string)shortcut.TargetPath))
                    throw new UserError("开始菜单中已有同名的其他入口。请先重命名该入口，再保存设置。");
                shortcut.TargetPath = paths.Executable;
                shortcut.WorkingDirectory = paths.Directory;
                shortcut.Description = "管理小狼毫的自动同步与重新部署";
                shortcut.Save();
            }
            catch (UserError) { throw; }
            catch
            {
                throw new UserError("程序已保存在这台电脑，但未能添加开始菜单入口。请检查安全软件或组织策略后重试。");
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
