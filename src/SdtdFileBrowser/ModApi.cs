using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace SdtdFileBrowser
{
    /// <summary>
    /// ModApi
    /// </summary>
    public class ModApi : IModApi
    {
        /// <summary>
        /// InitMod
        /// </summary>
        /// <param name="modInstance"></param>
        public void InitMod(Mod modInstance)
        {
            try
            {
                string path = Path.Combine(modInstance.Path, "appsettings.json");
                string jsonContent = File.ReadAllText(path, Encoding.UTF8);
                JObject jsonObject = JObject.Parse(jsonContent);

                int? port = jsonObject["Port"]?.ToObject<int>();
                if(port.HasValue == false)
                {
                    throw new Exception("FileBrowserPort not found.");
                }

                string? userName = jsonObject["UserName"]?.ToObject<string>();
                if (string.IsNullOrEmpty(userName))
                {
                    throw new Exception("UserName not found.");
                }

                string? password = jsonObject["Password"]?.ToObject<string>();
                if(string.IsNullOrEmpty(password))
                {
                    throw new Exception("Password not found.");
                }

                string binPath = Path.Combine(modInstance.Path, "3rdparty-binaries", "filebrowser");
                LoadFileBrowser(binPath, port.Value, AppContext.BaseDirectory, userName!, password!);

                CustomLogger.Info("Initialize mod: " + modInstance.Name + " success.");
                CustomLogger.Info("File browser listening on port: " + port.Value);
            }
            catch (Exception ex)
            {
                throw new Exception("Initialize mod: " + modInstance.Name + " failed.", ex);
            }
        }

        #region Load FileBrowser

        private static Process SilentStartProcess(string fileName, string argument)
        {
            string directory = Path.GetDirectoryName(fileName);

            var process = new Process();
            var startInfo = process.StartInfo;
            startInfo.FileName = fileName;
            startInfo.WorkingDirectory = directory;
            startInfo.Arguments = argument;
            startInfo.ErrorDialog = false;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden; // 与CreateNoWindow联合使用可以隐藏进程运行的窗体
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            process.Start();
            return process;
        }

        private static void LoadFileBrowser(string binPath, int port, string root, string username, string password)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                binPath += ".exe";
            }
            else
            {
                Process.Start("chmod", " +x \"" + binPath + "\"").WaitForExit();
            }

            if (File.Exists(binPath) == false)
            {
                throw new NotImplementedException("FileBrowser not found.");
            }

            string[] arguments = new string[]
            {
                "config init",
                "config set --address 0.0.0.0",
                $"config set --port {port}",
                "config set --locale zh-cn",
                $"config set --root \"{root}\"",
                $"users add {username} {password} --perm.admin --lockPassword",
                $"users update {username} --password {password} --perm.admin --lockPassword"
            };

            Process process;
            foreach (var argument in arguments)
            {
                process = SilentStartProcess(binPath, argument);
                if (process.HasExited == false)
                {
                    process.WaitForExit(3000);
                }
            }

            process = SilentStartProcess(binPath, string.Empty);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (process.HasExited == false)
                {
                    try
                    {
                        process.WaitForExit(3000); // 等待进程结束
                        if (process.HasExited == false)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            };

            if (isWindows)
            {
                ChildProcessTracker.AddProcess(process);
            }
        }
        #endregion
    }
}
