using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using System.IO;
using TidyManaged;
using System.Windows.Forms;
using System.Diagnostics;
using System.Web;
using System.Threading;

namespace CHM2HTML
{
    class Program
    {
        static string sourceCHM = "";
        static string distDir = "";//输出目录\
        static string tmpDir = "";//临时目录

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("请选择CHM文件");
            Thread.Sleep(500);
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CHM文件|*.chm";
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                sourceCHM = openFileDialog.FileName;
                Console.WriteLine("已选择文件：{0}", sourceCHM);
            }
            else
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("请选择输出目录");
            Thread.Sleep(500);
            var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                distDir = folderBrowserDialog.SelectedPath;
                Console.WriteLine("输出目录：{0}", distDir);
            }
            else
            {
                return;
            }

            tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(tmpDir))
            {
                Directory.CreateDirectory(tmpDir);
            }

            Console.WriteLine();
            Console.WriteLine("正在解析文件...");

            DecompileCHM();

            var html = ParseHtml();
            var menus = GetMenus(html);
            var sb = new StringBuilder();
            sb.Append("<ul id='nav-menu' class='menus'>");
            foreach (var menu in menus)
            {
                GenerateMenus(menu, sb);
            }
            sb.Append("</ul>");

            html = CHM2HTML.Properties.Resources.Template.Replace("<%Menus%>", sb.ToString());
            File.WriteAllText(Path.Combine(tmpDir, "index.html"), html, Encoding.UTF8);
            CopyToDist();
            Process.Start(distDir);

            Console.WriteLine();
            Console.WriteLine("解析完成");
            Thread.Sleep(1000);
        }

        static void DecompileCHM()
        {
            var cmd = String.Format("hh -decompile {0} {1}", tmpDir, sourceCHM);
            RunCmd(cmd);
        }

        //拷贝到目录
        static void CopyToDist()
        {
            DirectoryCopy(tmpDir, distDir, true);
            try
            {
                Directory.Delete(tmpDir, true);
            }
            catch { }
        }

        //生成菜单
        static void GenerateMenus(Menu menu, StringBuilder sb)
        {
            sb.Append("<li class='menu-item'>");
            sb.AppendFormat("<a href='javascript:' onclick=\"openPage('{0}');\">{1}</a>", menu.Url, menu.Name);
            if (menu.Children != null && menu.Children.Count > 0)
            {
                sb.Append("<ul class='menus sub-menus'>");
                foreach (var childMenu in menu.Children)
                {
                    GenerateMenus(childMenu, sb);
                }
                sb.Append("</ul>");
            }
            sb.Append("</li>");
        }

        static string ParseHtml()
        {
            var files = Directory.GetFiles(tmpDir, "*.hhc");
            if (files == null | files.Length == 0) return "";
            var file = files[0];
            var output = String.Empty;
            var type = EncodingType.GetType(file);
            var html = File.ReadAllText(file, type);
            html = EncodeChinese(html);
            using (Document doc = Document.FromString(html))
            {
                doc.ShowWarnings = false;
                doc.Quiet = true;
                doc.OutputXhtml = true;
                doc.CleanAndRepair();
                output = doc.Save();
            }
            return output;
        }

        static List<Menu> GetMenus(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var menus = new List<Menu>();
            var nodes = doc.DocumentNode.SelectNodes("//body/ul/li");
            if (nodes.Count > 0)
            {
                foreach (var node in nodes)
                {
                    var menu = new Menu();
                    GetChildMenu(node, menu);
                    menus.Add(menu);
                }
            }

            return menus;
        }

        static void GetChildMenu(HtmlNode node, Menu menu)
        {
            var nameNode = node.SelectSingleNode("object/param[@name='Name']");
            var valueNode = node.SelectSingleNode("object/param[@name='Local']");
            if (nameNode != null)
            {
                menu.Name = HttpUtility.UrlDecode(nameNode.GetAttributeValue("value", ""));
            }
            if (valueNode != null)
            {
                menu.Url = HttpUtility.UrlDecode(valueNode.GetAttributeValue("value", ""));
            }

            var childNodes = node.SelectNodes("ul/li");
            if (childNodes == null || childNodes.Count == 0) return;
            foreach (var childNode in childNodes)
            {
                var childMenu = new Menu();
                GetChildMenu(childNode, childMenu);
                menu.Children.Add(childMenu);
            }
        }

        static string EncodeChinese(string input)
        {
            var output = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if ((int)c > 127)
                {
                    output.Append(HttpUtility.UrlEncode(c.ToString()));
                }
                else
                {
                    output.Append(c);
                }
            }
            return output.ToString();
        }


        static void RunCmd(string cmd)
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo("cmd.exe");
            p.StartInfo.Arguments = String.Format(" /C {0} ", cmd);
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            p.WaitForExit();
            return;
        }

        static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}
