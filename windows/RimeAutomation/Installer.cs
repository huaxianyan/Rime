using System;
using System.IO;

namespace RimeAutomation
{
    internal sealed class Installer
    {
        private readonly AppPaths paths;
        public Installer(AppPaths paths) { this.paths = paths; }
        public void Install(string source)
        {
            if (paths.IsInstalledExecutable(source)) return;
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
            }
        }
    }
}
