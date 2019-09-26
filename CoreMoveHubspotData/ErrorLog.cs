using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoveHubspotToOntraport
{
    public class ErrorLog
    {
        public static string workingDirectory = Environment.CurrentDirectory;
        public static string projectDirectory = Directory.GetParent(workingDirectory).Parent.FullName;
        public static void WriteLogFile(string errorMsg, string callUrl, string module, string reqBody = "")
        {
            var line = Environment.NewLine + Environment.NewLine;

            var folderPath = $"{projectDirectory}/ErrorLog/";

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                var filePath = Path.Combine(folderPath, $"logfile_{DateTime.Now.ToString("MM-dd-yyyy")}.txt");
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose();
                }
                using (StreamWriter sw = File.AppendText(filePath))
                {
                    string error = "Log Written Date:" + " " + DateTime.Now.ToString() + line + "Error Module:" + " " + module + line + "Error Message:" + " " + errorMsg + line + "Erro API Url:" + " " + callUrl + line + "Req Body:" + " " + reqBody + line;
                    sw.WriteLine("-----------Exception Details on " + " " + DateTime.Now.ToString() + "-----------------");
                    sw.WriteLine("-------------------------------------------------------------------------------------");
                    sw.WriteLine(line);
                    sw.WriteLine(error);
                    sw.WriteLine("--------------------------------*End*------------------------------------------");
                    sw.WriteLine(line);
                    sw.Flush();
                    sw.Close();
                }

            }
            catch (Exception e)
            {
                e.ToString();
            }
        }

        public static void InfoMessage(string message)
        {
            var line = Environment.NewLine;
            var folderPath = $"{projectDirectory}/ErrorLog/";

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var filePath = Path.Combine(folderPath, $"logfile_{DateTime.Now.ToString("MM-dd-yyyy")}.txt");
                
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose();
                }
                using (StreamWriter sw = File.AppendText(filePath))
                {
                    sw.WriteLine();
                    sw.WriteLine(message);
                    sw.Flush();
                    sw.Close();
                }
            }
            catch (Exception e)
            {
                e.ToString();
            }
        }
    }
}
