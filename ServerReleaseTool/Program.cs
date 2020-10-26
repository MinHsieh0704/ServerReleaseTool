using Min_Helpers;
using Min_Helpers.LogHelper;
using Min_Helpers.PrintHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerReleaseTool
{
    class Program
    {
        static Print PrintService { get; set; } = null;
        static Log LogService { get; set; } = null;

        [STAThread]
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");

            try
            {
                LogService = new Log();
                PrintService = new Print(LogService);

                LogService.Write("");
                PrintService.Log("App Start", Print.EMode.info);

                PrintService.NewLine();

                FolderBrowserDialog dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                string basePath = dialog.SelectedPath;
                string workspacePath = $"{basePath}\\workspace";
                string nsisPath = $"{workspacePath}\\nsis";

                PrintService.Write("Project Folder: ", Print.EMode.info);
                PrintService.WriteLine(basePath, Print.EMode.message);

                if (!Directory.Exists(workspacePath)) 
                    throw new Exception($"workspace folder not found");
                if (!File.Exists($"{workspacePath}\\package.json")) 
                    throw new Exception($"package.json not found");

                JObject packageJson = JsonConvert.DeserializeObject<JObject>(File.ReadAllText($"{workspacePath}\\package.json"));
                string projectName = packageJson["name"].ToString();
                string projectVersion = packageJson["version"].ToString();
                string projectDisplayName = packageJson["config"]["displayname"].ToString();
                string projectDescription = packageJson["description"].ToString();

                PrintService.Write("Project Name: ", Print.EMode.info);
                PrintService.WriteLine(projectName, Print.EMode.message);

                PrintService.Write("Project Version: ", Print.EMode.info);
                PrintService.WriteLine(projectVersion, Print.EMode.message);

                PrintService.Write("Project Display Name: ", Print.EMode.info);
                PrintService.WriteLine(projectDisplayName, Print.EMode.message);

                PrintService.Write("Project Description: ", Print.EMode.info);
                PrintService.WriteLine(projectDescription, Print.EMode.message);

                PrintService.NewLine();

                if (Directory.Exists(nsisPath))
                {
                    if (!File.Exists($"{nsisPath}\\_define.nsh"))
                        throw new Exception($"_define.nsh not found");
                    if (!File.Exists($"{nsisPath}\\service-install.bat"))
                        throw new Exception($"service-install.bat not found");
                    if (!File.Exists($"{nsisPath}\\service-restart.bat"))
                        throw new Exception($"service-restart.bat not found");
                    if (!File.Exists($"{nsisPath}\\service-uninstall.bat"))
                        throw new Exception($"service-uninstall.bat not found");
                }

                PrintService.Write("Please Input New Verson: ", Print.EMode.question);
                string projectNewVersion = Console.ReadLine();
                PrintService.NewLine();

                List<string> paths = new List<string>()
                {
                    $"{basePath}\\package.json",
                    $"{basePath}\\package-lock.json",
                    $"{workspacePath}\\package.json",
                    $"{workspacePath}\\package-lock.json"
                };

                for (int i = 0; i < paths.Count; i++)
                {
                    if (File.Exists(paths[i]))
                    {
                        string path = Path.GetFileName(paths[i]);

                        JObject input = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(paths[i]));

                        if (input["name"] != null) input["name"] = projectName;
                        if (input["version"] != null) input["version"] = projectNewVersion;
                        if (input["config"]?["displayname"] != null) input["config"]["displayname"] = projectDisplayName;
                        if (input["description"] != null) input["description"] = projectDisplayName;

                        File.WriteAllText(paths[i], JsonConvert.SerializeObject(input, Formatting.Indented));

                        PrintService.WriteLine($"Update \"{path}\" Success", Print.EMode.success);
                    }
                }

                if (Directory.Exists(nsisPath))
                {
                    paths = new List<string>()
                    {
                        $"{nsisPath}\\_define.nsh"
                    };

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (File.Exists(paths[i]))
                        {
                            string path = Path.GetFileName(paths[i]);

                            string input = File.ReadAllText(paths[i]);

                            string content = new Regex($"^!define PRODUCT_VERSION .*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Replace(input, $"!define PRODUCT_VERSION \"{projectNewVersion}\"", 1);

                            File.WriteAllText(paths[i], content);
                            PrintService.WriteLine($"Update \"{path}\" Success", Print.EMode.success);
                        }
                    }

                    paths = new List<string>()
                    {
                        $"{nsisPath}\\service-install.bat",
                        $"{nsisPath}\\service-restart.bat",
                        $"{nsisPath}\\service-uninstall.bat",
                    };

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (File.Exists(paths[i]))
                        {
                            string path = Path.GetFileName(paths[i]);

                            string input = File.ReadAllText(paths[i]);

                            string content = input;
                            content = new Regex($"^set service=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Replace(content, $"set service={projectName}", 1);
                            content = new Regex($"^set displayName=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Replace(content, $"set displayName={projectDisplayName}", 1);
                            content = new Regex($"^set description=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Replace(content, $"set description={projectDisplayName} v{projectNewVersion}", 1);

                            File.WriteAllText(paths[i], content);
                            PrintService.WriteLine($"Update \"{path}\" Success", Print.EMode.success);
                        }
                    }
                }

                PrintService.NewLine();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.Start();

                    string root = Directory.GetDirectoryRoot(basePath);

                    process.StandardInput.WriteLine(root.Replace("\\", ""));
                    Thread.Sleep(10);

                    process.StandardInput.WriteLine($"cd {workspacePath}");
                    Thread.Sleep(10);

                    process.StandardInput.WriteLine("git add package.json");
                    Thread.Sleep(10);

                    process.StandardInput.WriteLine("git add package-lock.json");
                    Thread.Sleep(10);

                    process.StandardInput.WriteLine("git add nsis/.");
                    Thread.Sleep(10);

                    process.StandardInput.WriteLine($"git commit -m \"v{projectNewVersion}\"");
                    Thread.Sleep(10);

                    PrintService.WriteLine($"Git Commit Success", Print.EMode.success);
                    Thread.Sleep(10);

                    if (File.Exists($"{workspacePath}\\change.log"))
                        File.Delete($"{workspacePath}\\change.log");

                    string count = "5000";

                    process.StandardInput.WriteLine($"git log --date=format:\"%Y/%m/%d %H:%M:%S\" --pretty=format:\"%cd -> author: %<(10,trunc)%an, message: %s\" > \".\\change.log\" -{count}");
                    while (!File.Exists($"{workspacePath}\\change.log")) Thread.Sleep(10);

                    process.StandardInput.WriteLine($"git add change.log");
                    process.StandardInput.WriteLine($"git commit --amend --no-edit");
                    Thread.Sleep(10);

                    PrintService.WriteLine($"Git Log Export Success", Print.EMode.success);
                }

                PrintService.NewLine();

                if (Directory.Exists(nsisPath))
                {
                    PrintService.Write("Build to NSIS Installer? [y/N] ", Print.EMode.question);
                    string build = Console.ReadLine();
                    PrintService.NewLine();

                    bool isBuild = !(string.IsNullOrEmpty(build) || build.ToLower() == "n" || build.ToLower() == "no");

                    if (isBuild)
                    {
                        paths = new List<string>()
                        {
                            $"{nsisPath}\\server-full.nsi",
                            $"{nsisPath}\\server-workspace.nsi",
                            $"{nsisPath}\\server.nsi",
                        };

                        string nsisFull = paths.Where((n) => n.IndexOf("full.nsi") > -1).ToList().First();
                        string nsisWorkspace = paths.Where((n) => n.IndexOf("workspace.nsi") > -1).ToList().First();
                        string nsis = paths.Where((n) => n.IndexOf(".nsi") > -1 && n != nsisFull && n != nsisWorkspace).ToList().First();

                        string makensis = ConfigurationManager.AppSettings["makensis"];

                        using (CancellationTokenSource tokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                CancellationToken token = tokenSource.Token;

                                if (File.Exists(nsis))
                                {
                                    string path = Path.GetFileName(nsis);

                                    bool isError = false;

                                    using (Process process = new Process())
                                    {
                                        PrintService.WriteLine($"Build \"{path}\" Start", Print.EMode.info);

                                        process.StartInfo.FileName = makensis;
                                        process.StartInfo.Arguments = nsis;
                                        process.StartInfo.UseShellExecute = false;
                                        process.StartInfo.RedirectStandardOutput = true;
                                        process.StartInfo.RedirectStandardError = true;
                                        process.StartInfo.CreateNoWindow = true;

                                        process.EnableRaisingEvents = true;

                                        Observable
                                            .FromEventPattern<DataReceivedEventArgs>(process, "OutputDataReceived")
                                            .Select((x) => x.EventArgs.Data)
                                            .Where((x) => x != null)
                                            .DistinctUntilChanged()
                                            .Subscribe((x) =>
                                            {
                                                PrintService.WriteLine(x, Print.EMode.message);
                                            });
                                        Observable
                                            .FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                                            .Select((x) => x.EventArgs.Data)
                                            .Where((x) => x != null)
                                            .DistinctUntilChanged()
                                            .Subscribe((x) =>
                                            {
                                                PrintService.WriteLine(x, Print.EMode.error);
                                                isError = true;
                                            });

                                        process.Start();

                                        process.BeginOutputReadLine();
                                        process.BeginErrorReadLine();

                                        process.WaitForExit();

                                        if (isError)
                                        {
                                            PrintService.NewLine();
                                            throw new Exception($"Build \"{path}\" Fail");
                                        }

                                        PrintService.WriteLine($"Build \"{path}\" Success", Print.EMode.success);
                                    }
                                }

                                tokenSource.Cancel();
                            }
                            catch (Exception)
                            {
                                tokenSource.Cancel();
                                throw;
                            }
                        }

                        PrintService.NewLine();

                        using (CancellationTokenSource tokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                CancellationToken token = tokenSource.Token;

                                if (File.Exists(nsisWorkspace))
                                {
                                    string path = Path.GetFileName(nsisWorkspace);

                                    bool isError = false;

                                    using (Process process = new Process())
                                    {
                                        PrintService.WriteLine($"Build \"{path}\" Start", Print.EMode.info);

                                        process.StartInfo.FileName = makensis;
                                        process.StartInfo.Arguments = nsisWorkspace;
                                        process.StartInfo.UseShellExecute = false;
                                        process.StartInfo.RedirectStandardOutput = true;
                                        process.StartInfo.RedirectStandardError = true;
                                        process.StartInfo.CreateNoWindow = true;

                                        process.EnableRaisingEvents = true;

                                        Observable
                                            .FromEventPattern<DataReceivedEventArgs>(process, "OutputDataReceived")
                                            .Select((x) => x.EventArgs.Data)
                                            .Where((x) => x != null)
                                            .DistinctUntilChanged()
                                            .Subscribe((x) =>
                                            {
                                                PrintService.WriteLine(x, Print.EMode.message);
                                            });
                                        Observable
                                            .FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                                            .Select((x) => x.EventArgs.Data)
                                            .Where((x) => x != null)
                                            .DistinctUntilChanged()
                                            .Subscribe((x) =>
                                            {
                                                PrintService.WriteLine(x, Print.EMode.error);
                                                isError = true;
                                            });

                                        process.Start();

                                        process.BeginOutputReadLine();
                                        process.BeginErrorReadLine();

                                        process.WaitForExit();

                                        if (isError)
                                        {
                                            PrintService.NewLine();
                                            throw new Exception($"Build \"{path}\" Fail");
                                        }

                                        PrintService.WriteLine($"Build \"{path}\" Success", Print.EMode.success);
                                    }
                                }

                                tokenSource.Cancel();
                            }
                            catch (Exception)
                            {
                                tokenSource.Cancel();
                                throw;
                            }
                        }

                        PrintService.NewLine();

                        using (CancellationTokenSource tokenSource = new CancellationTokenSource())
                        {
                            try
                            {
                                CancellationToken token = tokenSource.Token;

                                if (File.Exists(nsisFull))
                                {
                                    string path = Path.GetFileName(nsisFull);

                                    bool isError = false;

                                    using (Process process = new Process())
                                    {
                                        PrintService.WriteLine($"Build \"{path}\" Start", Print.EMode.info);

                                        process.StartInfo.FileName = makensis;
                                        process.StartInfo.Arguments = nsisFull;
                                        process.StartInfo.UseShellExecute = false;
                                        process.StartInfo.RedirectStandardOutput = true;
                                        process.StartInfo.RedirectStandardError = true;
                                        process.StartInfo.CreateNoWindow = true;

                                        process.EnableRaisingEvents = true;

                                        Observable
                                            .FromEventPattern<DataReceivedEventArgs>(process, "OutputDataReceived")
                                            .Select((x) => x.EventArgs.Data)
                                            .Where((x) => x != null)
                                            .DistinctUntilChanged()
                                            .Subscribe((x) =>
                                            {
                                                PrintService.WriteLine(x, Print.EMode.message);
                                            });
                                        Observable
                                            .FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                                            .Select((x) => x.EventArgs.Data)
                                            .Where((x) => x != null)
                                            .DistinctUntilChanged()
                                            .Subscribe((x) =>
                                            {
                                                PrintService.WriteLine(x, Print.EMode.error);
                                                isError = true;
                                            });

                                        process.Start();

                                        process.BeginOutputReadLine();
                                        process.BeginErrorReadLine();

                                        process.WaitForExit();

                                        if (isError)
                                        {
                                            PrintService.NewLine();
                                            throw new Exception($"Build \"{path}\" Fail");
                                        }

                                        PrintService.WriteLine($"Build \"{path}\" Success", Print.EMode.success);
                                    }
                                }

                                tokenSource.Cancel();
                            }
                            catch (Exception)
                            {
                                tokenSource.Cancel();
                                throw;
                            }
                        }

                        PrintService.NewLine();

                        Process.Start($"{nsisPath}//Release");
                    }
                }
            }
            catch (Exception ex)
            {
                ex = ExceptionHelper.GetReal(ex);
                PrintService.Log($"App Error, {ex.Message}", Print.EMode.error);
            }
            finally
            {
                PrintService.Log("App End", Print.EMode.info);
                Console.ReadKey();

                Environment.Exit(0);
            }
        }
    }
}
