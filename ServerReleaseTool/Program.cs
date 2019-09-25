using Min_Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServerReleaseTool
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
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

                ConsoleHelper.ConsoleMessage2("Project Folder: ", ConsoleHelper.MessageColor.info);
                ConsoleHelper.ConsoleMessage1(basePath);

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

                ConsoleHelper.ConsoleMessage2("Project Name: ", ConsoleHelper.MessageColor.info);
                ConsoleHelper.ConsoleMessage1(projectName);

                ConsoleHelper.ConsoleMessage2("Project Version: ", ConsoleHelper.MessageColor.info);
                ConsoleHelper.ConsoleMessage1(projectVersion);

                ConsoleHelper.NewLine();

                ConsoleHelper.ConsoleMessage2("Please Input New Verson: ", ConsoleHelper.MessageColor.question);
                string projectNewVersion = Console.ReadLine();

                List<string> paths = new List<string>()
                {
                    $"{basePath}\\package.json",
                    $"{basePath}\\package-lock.json",
                    $"{workspacePath}\\package.json",
                    $"{workspacePath}\\package-lock.json"
                };

                paths.AddRange(Directory.GetFiles(nsisPath, "*.nsi"));

                for (int i = 0; i < paths.Count; i++)
                {
                    if (File.Exists(paths[i]))
                    {
                        string content = File.ReadAllText(paths[i]).Replace(projectVersion, projectNewVersion);
                        File.WriteAllText(paths[i], content);
                        ConsoleHelper.ConsoleMessage1($"Update \"{paths[i]}\" Success", ConsoleHelper.MessageColor.success);
                    }
                }

                ConsoleHelper.NewLine();

                string nsisFull = paths.Where((n) => n.IndexOf("full.nsi") > -1).ToList().First();
                string nsisWorkspace = paths.Where((n) => n.IndexOf("workspace.nsi") > -1).ToList().First();
                string nsis = paths.Where((n) => n.IndexOf(".nsi") > -1 && n != nsisFull && n != nsisWorkspace).ToList().First();

                string makensis = ConfigurationManager.AppSettings["makensis"];

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = makensis;
                    process.StartInfo.Arguments = nsis;

                    process.Start();
                    process.WaitForExit();
                    ConsoleHelper.ConsoleMessage1($"Build \"{nsis}\" Success", ConsoleHelper.MessageColor.success);
                }

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = makensis;
                    process.StartInfo.Arguments = nsisWorkspace;

                    process.Start();
                    process.WaitForExit();
                    ConsoleHelper.ConsoleMessage1($"Build \"{nsisWorkspace}\" Success", ConsoleHelper.MessageColor.success);
                }

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = makensis;
                    process.StartInfo.Arguments = nsisFull;

                    process.Start();
                    process.WaitForExit();
                    ConsoleHelper.ConsoleMessage1($"Build \"{nsisFull}\" Success", ConsoleHelper.MessageColor.success);
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
                    process.StandardInput.WriteLine($"cd {workspacePath}");
                    process.StandardInput.WriteLine("git add package.json");
                    process.StandardInput.WriteLine("git add package-lock.json");
                    process.StandardInput.WriteLine("git add nsis/.");
                    process.StandardInput.WriteLine($"git commit -m \"v{projectNewVersion}\"");

                    ConsoleHelper.ConsoleMessage1($"Git Commit Success", ConsoleHelper.MessageColor.success);
                }

                Process.Start($"{nsisPath}//Release");
            }
            catch (Exception ex)
            {
                ConsoleHelper.NewLine();

                ex = ExceptionHelper.GetRealException(ex);
                ConsoleHelper.ConsoleMessage1($"Error: {ex.Message}", ConsoleHelper.MessageColor.error);
            }
            finally
            {
                ConsoleHelper.NewLine();

                ConsoleHelper.ConsoleMessage1("Please Enter Any Key to Exit", ConsoleHelper.MessageColor.info);
                Console.ReadKey();
            }

            return 0;
        }
    }
}
