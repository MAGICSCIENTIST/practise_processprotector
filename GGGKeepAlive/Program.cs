using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Threading;

namespace GGGKeepAlive
{
    class Program
    {
        private static ConsoleColor succeedColor = ConsoleColor.Green;
        private static ConsoleColor errColor = ConsoleColor.Red;
        private static ConsoleColor warnColor = ConsoleColor.Yellow;
        private static ConsoleColor titleFColor = ConsoleColor.White;
        private static ConsoleColor titleBColor = ConsoleColor.Black;
        private static List<ProtectProgress> _needProtectProcessList = new List<ProtectProgress>();
        private static int _round = 0;
        static object locker = new object();


        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int hConsoleHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);



        static void Main(string[] args)
        {
            Process curProcess = System.Diagnostics.Process.GetCurrentProcess();
            //only one cmd on the same time
            if (Process.GetProcesses().Any(x => x.ProcessName == curProcess.ProcessName && x.Id != curProcess.Id))
            {
                return;
            }

            if (!initNeedProtectProcess())
            {
                return;
            }

            WriteTitle("守护列表:");
            WriteSucceed("name              cmd");
            WriteSucceed(_needProtectProcessList.Select(x => $"{x.Describe}     {x.Command}\n").ToList());


            if (!fuckQuickMode())
            {
                WriteWarn("未取消快速编辑模式");
            }



            System.Timers.Timer timer;
            timer = new System.Timers.Timer();
            int Interval = Convert.ToInt32(ConfigurationManager.AppSettings["interval"]);
            timer.Interval = Interval == 0 ? 10000 : Interval;//设置计时器事件间隔执行时间            
            timer.Elapsed += new System.Timers.ElapsedEventHandler((sender, eventArgs) =>
            {
                if (Monitor.TryEnter(locker))
                {
                    try
                    {
                        StartProtect();
                    }
                    finally
                    {
                        Monitor.Exit(locker);
                    }
                }
            });
            timer.Enabled = true;

            //ctrl + c 等按键取消
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitEvent.Set();
            };
            exitEvent.WaitOne();
            //其他input 取消
            do
            {
                Thread.Sleep(2000);
                while (!Console.KeyAvailable)
                {
                    // Do something
                }
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
            Console.ReadLine();
        }

        #region write
        private static void WriteTitle(string text)
        {
            WriteTitle(new List<string>() { text });
        }

        private static void WriteSucceed(string text)
        {
            WriteSucceed(new List<string>() { text });
        }

        private static void WriteSucceed(List<string> textList)
        {
            Console.ForegroundColor = succeedColor;
            textList.ForEach(Console.WriteLine);
            Console.ResetColor();
        }

        private static void WriteInfo(string text)
        {
            WriteInfo(new List<string>() { text });
        }

        private static void WriteInfo(List<string> textList)
        {
            //Console.ForegroundColor = errColor;
            textList.ForEach(Console.WriteLine);
            //Console.ResetColor();
        }

        private static void WriteWarn(string text)
        {
            WriteWarn(new List<string>() { text });
        }

        private static void WriteWarn(List<string> textList)
        {
            Console.ForegroundColor = warnColor;
            textList.ForEach(Console.WriteLine);
            Console.ResetColor();
        }
        private static void WriteError(string text)
        {
            WriteError(new List<string>() { text });
        }
        private static void WriteError(List<string> textList)
        {
            Console.ForegroundColor = errColor;
            textList.ForEach(Console.WriteLine);
            Console.ResetColor();
        }
        private static void WriteTitle(List<string> textList)
        {
            Console.ForegroundColor = titleFColor;
            Console.BackgroundColor = titleBColor;
            WriteSplitLine();
            textList.ForEach(Console.WriteLine);
            WriteSplitLine();
            Console.ResetColor();
        }

        private static void WriteSplitLine()
        {
            Console.WriteLine("=======================================");
        }

        #endregion

        private static bool fuckQuickMode()
        {
            const uint ENABLE_QUICK_EDIT_MODE = 0x0040;

            // get current console mode
            uint mode;
            IntPtr consoleHandle = GetStdHandle(-10);
            if (!GetConsoleMode(consoleHandle, out mode))
            {
                // Error: Unable to get console mode.
                return false;
            }

            // Clear the quick edit bit in the mode flags
            mode &= ~ENABLE_QUICK_EDIT_MODE;


            // set the new mode
            if (!SetConsoleMode(consoleHandle, mode))
            {
                // ERROR: Unable to set console mode
                return false;
            }
            return true;
        }

        private static bool initNeedProtectProcess()
        {
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.Count == 0)
            {
                using (EventLog eventLog = new EventLog(System.AppDomain.CurrentDomain.FriendlyName))
                {
                    eventLog.Source = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                    eventLog.WriteEntry($"没有配置守护项目", EventLogEntryType.Error, 500, 1);
                }
                return false;
            }
            foreach (var key in appSettings.AllKeys)
            {
                if (key.Contains("keep_"))
                {
                    string name = key.Substring(key.IndexOf("keep_", StringComparison.Ordinal) + 5);
                    _needProtectProcessList.Add(new ProtectProgress()
                    {
                        Describe = name,
                        Command = appSettings[key],
                        ProtectCount = 0
                    });
                }
            }


            return true;
        }

        private static void StartProtect()
        {

            try
            {
                WriteTitle($"{DateTime.Now.ToString()}, 开始第{++_round}次轮回");

                //一些没有初始化的可以初始化了
                Process[] processIdAry = Process.GetProcesses();

                foreach (ProtectProgress protectProgress in _needProtectProcessList)
                {
                    WriteInfo($"开始验证{protectProgress.Describe}");
                    //说明已经启动了
                    if (protectProgress.Process != null && processIdAry.Any(x => x.Id == protectProgress.Process.Id))
                    {
                        WriteSucceed($"验证成功, {protectProgress.Describe} 还活着");
                        continue;
                    }
                    using (EventLog eventLog = new EventLog(System.AppDomain.CurrentDomain.FriendlyName))
                    {
                        eventLog.Source = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                        string log = $"进程失踪,开始守护进程,命令: {protectProgress.Command}";
                        eventLog.WriteEntry(log, EventLogEntryType.Information, 101, 1);
                        WriteWarn(log);
                    }
                    try
                    {
                        protectProgress.Process = startCmd(protectProgress.Command);
                        if (protectProgress.Process != null)
                        {
                            protectProgress.ProtectCount++;
                            using (EventLog eventLog = new EventLog(System.AppDomain.CurrentDomain.FriendlyName))
                            {
                                eventLog.Source = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                                string log = $"命令:{protectProgress.Command}守护成功,当前第{protectProgress.ProtectCount}次. ID:{protectProgress.Process.Id} NAME:{protectProgress.Process.ProcessName}";
                                eventLog.WriteEntry(log, EventLogEntryType.Information, 200, 1);
                                WriteSucceed(log);
                            }
                            protectProgress.Name = protectProgress.Process.ProcessName;
                        }
                    }
                    catch (Exception ex)
                    {
                        using (EventLog eventLog = new EventLog(System.AppDomain.CurrentDomain.FriendlyName))
                        {
                            eventLog.Source = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                            string log = $"{protectProgress.Describe} 命令:{protectProgress.Command}守护失败,{ex.Message}";
                            eventLog.WriteEntry(log, EventLogEntryType.Error, 500, 1);
                            WriteError(log);
                        }
                    }
                }

            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
            finally
            {
                GC.Collect();
            }
        }



        private static Process startCmd(string cmd)
        {
            var processStartInfo = new ProcessStartInfo("cmd.exe", "/c " + cmd);
            processStartInfo.CreateNoWindow = false;
            Process P = Process.Start(processStartInfo);
            return P;
        }
    }

    public class ProtectProgress
    {
        //public int id { get; set; }
        public string Command { get; set; }
        public string Describe { get; set; }
        public string Name { get; set; }
        //public bool IsStarted { get; set; }
        public int ProtectCount { get; set; }
        public Process Process { get; set; }
    }
}
