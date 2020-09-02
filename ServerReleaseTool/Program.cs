using Min_Helpers;
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
        [STAThread]
        static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");

            ConsoleHelper.Initialize();

            try
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return 0;
                }

                string basePath = dialog.SelectedPath;
                string workspacePath = $"{basePath}\\workspace";
                string nsisPath = $"{workspacePath}\\nsis";

                ConsoleHelper.Write("Project Folder: ", ConsoleHelper.EMode.info);
                ConsoleHelper.WriteLine(basePath, ConsoleHelper.EMode.message);

                if (!Directory.Exists(workspacePath))
                {
                    throw new Exception($"workspace folder not found");
                }

                if (!Directory.Exists(nsisPath))
                {
                    throw new Exception($"nsis folder not found");
                }

                if (!File.Exists($"{workspacePath}\\package.json"))
                {
                    throw new Exception($"package.json not found");
                }

                JObject packageJson = JsonConvert.DeserializeObject<JObject>(File.ReadAllText($"{workspacePath}\\package.json"));
                string projectName = packageJson["config"]["displayname"].ToString();
                string projectVersion = packageJson["version"].ToString();

                ConsoleHelper.Write("Project Name: ", ConsoleHelper.EMode.info);
                ConsoleHelper.WriteLine(projectName, ConsoleHelper.EMode.message);

                ConsoleHelper.Write("Project Version: ", ConsoleHelper.EMode.info);
                ConsoleHelper.WriteLine(projectVersion, ConsoleHelper.EMode.message);

                ConsoleHelper.NewLine();

                ConsoleHelper.Write("Please Input New Verson: ", ConsoleHelper.EMode.question);
                string projectNewVersion = Console.ReadLine();

                ConsoleHelper.NewLine();

                ConsoleHelper.Write("Build to NSIS Installer? [y/N] ", ConsoleHelper.EMode.question);
                string build = Console.ReadLine();
                bool isBuild = !(string.IsNullOrEmpty(build) || build.ToLower() == "n" || build.ToLower() == "no");

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

                        string input = File.ReadAllText(paths[i]);
                        string content = new Regex($"\"version\": \"{projectVersion}\"").Replace(input, $"\"version\": \"{projectNewVersion}\"", 1);
                        File.WriteAllText(paths[i], content);
                        ConsoleHelper.WriteLine($"Update \"{path}\" Success", ConsoleHelper.EMode.success);
                    }
                }

                paths = Directory.GetFiles(nsisPath, "*.nsi").ToList();

                for (int i = 0; i < paths.Count; i++)
                {
                    if (File.Exists(paths[i]))
                    {
                        string path = Path.GetFileName(paths[i]);

                        string input = File.ReadAllText(paths[i]);
                        string content = new Regex($"!define PRODUCT_VERSION \"{projectVersion}\"").Replace(input, $"!define PRODUCT_VERSION \"{projectNewVersion}\"", 1);
                        File.WriteAllText(paths[i], content);
                        ConsoleHelper.WriteLine($"Update \"{path}\" Success", ConsoleHelper.EMode.success);
                    }
                }

                ConsoleHelper.NewLine();

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

                    ConsoleHelper.WriteLine($"Git Commit Success", ConsoleHelper.EMode.success);
                    Thread.Sleep(10);

                    ConsoleHelper.NewLine();

                    string count = "5000";

                    process.StandardInput.WriteLine($"git log --date=format:\"%Y/%m/%d %H:%M:%S\" --pretty=format:\"%cd -> author: %<(10,trunc)%an, message: %s\" > \".\\change.log\" -{count}");
                    Thread.Sleep(10);

                    process.StandardInput.WriteLine($"git add change.log");
                    process.StandardInput.WriteLine($"git commit --amend --no-edit");
                    Thread.Sleep(10);

                    ConsoleHelper.WriteLine($"Git Log Export Success", ConsoleHelper.EMode.success);
                }

                if (isBuild)
                {
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
                                    ConsoleHelper.WriteLine($"Build \"{path}\" Start", ConsoleHelper.EMode.info);

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
                                            ConsoleHelper.WriteLine(x, ConsoleHelper.EMode.message);
                                        });
                                    Observable
                                        .FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                                        .Select((x) => x.EventArgs.Data)
                                        .Where((x) => x != null)
                                        .DistinctUntilChanged()
                                        .Subscribe((x) =>
                                        {
                                            ConsoleHelper.WriteLine(x, ConsoleHelper.EMode.error);
                                            isError = true;
                                        });

                                    process.Start();

                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    process.WaitForExit();

                                    if (isError)
                                    {
                                        throw new Exception($"Build \"{path}\" Fail");
                                    }

                                    ConsoleHelper.WriteLine($"Build \"{path}\" Success", ConsoleHelper.EMode.success);
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

                    ConsoleHelper.NewLine();

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
                                    ConsoleHelper.WriteLine($"Build \"{path}\" Start", ConsoleHelper.EMode.info);

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
                                            ConsoleHelper.WriteLine(x, ConsoleHelper.EMode.message);
                                        });
                                    Observable
                                        .FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                                        .Select((x) => x.EventArgs.Data)
                                        .Where((x) => x != null)
                                        .DistinctUntilChanged()
                                        .Subscribe((x) =>
                                        {
                                            ConsoleHelper.WriteLine(x, ConsoleHelper.EMode.error);
                                            isError = true;
                                        });

                                    process.Start();

                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    process.WaitForExit();

                                    if (isError)
                                    {
                                        throw new Exception($"Build \"{path}\" Fail");
                                    }

                                    ConsoleHelper.WriteLine($"Build \"{path}\" Success", ConsoleHelper.EMode.success);
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

                    ConsoleHelper.NewLine();

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
                                    ConsoleHelper.WriteLine($"Build \"{path}\" Start", ConsoleHelper.EMode.info);

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
                                            ConsoleHelper.WriteLine(x, ConsoleHelper.EMode.message);
                                        });
                                    Observable
                                        .FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                                        .Select((x) => x.EventArgs.Data)
                                        .Where((x) => x != null)
                                        .DistinctUntilChanged()
                                        .Subscribe((x) =>
                                        {
                                            ConsoleHelper.WriteLine(x, ConsoleHelper.EMode.error);
                                            isError = true;
                                        });

                                    process.Start();

                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    process.WaitForExit();

                                    if (isError)
                                    {
                                        throw new Exception($"Build \"{path}\" Fail");
                                    }

                                    ConsoleHelper.WriteLine($"Build \"{path}\" Success", ConsoleHelper.EMode.success);
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

                    ConsoleHelper.NewLine();
                }

                Process.Start($"{nsisPath}//Release");
            }
            catch (Exception ex)
            {
                ConsoleHelper.NewLine();

                ex = ExceptionHelper.GetReal(ex);
                ConsoleHelper.WriteLine($"Error: {ex.Message}", ConsoleHelper.EMode.error);
            }
            finally
            {
                ConsoleHelper.NewLine();

                ConsoleHelper.WriteLine("Please Enter Any Key to Exit", ConsoleHelper.EMode.info);

                Console.ReadKey();
                Environment.Exit(0);
            }

            return 0;
        }
    }
}
