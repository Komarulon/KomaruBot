using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Models.Client;


namespace KomaruBot
{
    public class Logging
    {
        private static object mylock = new object();

        private static string getFilename()
        {
            return "log_" + DateTime.Now.ToString("yyyy_MM_dd") + ".txt";
        }

        public static void Initialize()
        {
            lock (mylock)
            {
                if (!Directory.Exists("logs/")) Directory.CreateDirectory("logs/");
                using (System.IO.StreamWriter sw = System.IO.File.AppendText("logs/" + getFilename()))
                {
                    try
                    {
                        sw.WriteLine("");
                        sw.WriteLine("----------------------------------------------");
                        sw.WriteLine("");
                    }
                    finally
                    {
                        sw.Close();
                    }
                }
            }
        }


        public static void LogMessage(string msg, bool isError = false)
        {
            lock (mylock)
            {

                using (System.IO.StreamWriter sw = System.IO.File.AppendText("logs/" + getFilename()))
                {
                    try
                    {
                        string logLine = System.String.Format(
                            "{0:G}: {1}.", System.DateTime.Now, msg);
                        sw.WriteLine(logLine);
                    }
                    finally
                    {
                        sw.Close();
                    }
                }

                if (isError)
                {
                    var color = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = color;
                }
                else
                {
                    Console.WriteLine(msg);
                }
            }
        }

        public static void LogException(Exception exc, string title)
        {
            try
            {
                string errorMsg = $"\r\n\r\n{title}: {exc.Message} \r\nStack Trace: {exc.StackTrace}";

                var curEx = exc.InnerException;
                int c = 1;
                while (curEx != null && c < 100)
                {
                    errorMsg = $"\r\n\r\nInner Exception # {c}: {exc.Message} \r\nStack Trace: {exc.StackTrace}";
                    c++;
                    curEx = curEx.InnerException;
                }

                LogMessage(errorMsg, true);
            }
            catch (Exception ex)
            {

                string errorMsg = $"\r\n\r\nEXCEPTION LOGGING EXCEPTION ({title}): {ex.Message} \r\nStack Trace: {ex.StackTrace}";

                var curEx = ex.InnerException;
                int c = 1;
                while (curEx != null && c < 100)
                {
                    errorMsg = $"\r\n\r\nInner Exception # {c}: {ex.Message} \r\nStack Trace: {ex.StackTrace}";
                    c++;
                    curEx = curEx.InnerException;
                }


                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(errorMsg);
                Console.ForegroundColor = color;
            }
        }

        public static void LogException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                LogException((Exception)e.ExceptionObject, "Unhandled Exception");
            }
            catch (Exception e2)
            {
                LogException(e2, "Unhandled Exception - section 2");
            }
        }
    }


    

}
