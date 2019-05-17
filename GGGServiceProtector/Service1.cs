using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace GGGServiceProtector
{
    /// <summary>
    /// windows service  保证某些东西一定活着
    /// </summary>
    public partial class Service1 : ServiceBase
    {
        private List<ProtectProgress> _needProtectProcessList = new List<ProtectProgress>();

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // string appStartPath= @"C:\Users\Administrator\Desktop\okl1\okl1.exe";                        
            _needProtectProcessList.Add(new ProtectProgress()
            {
                Command = "ipconfig&pause",
                ProtectCount = 0
            });

            System.Timers.Timer timer;

            timer = new System.Timers.Timer();
            timer.Interval = 10000;//设置计时器事件间隔执行时间
            timer.Elapsed += new System.Timers.ElapsedEventHandler(StartProtect);
            timer.Enabled = true;         
        }

        protected override void OnStop()
        {
        }


        private void StartProtect(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {

                //一些没有初始化的可以初始化了
                Process[] processIdAry = Process.GetProcesses();

                foreach (ProtectProgress protectProgress in _needProtectProcessList)
                {
                    //说明已经启动了
                    if (protectProgress.Process != null && processIdAry.Any(x => x.Id == protectProgress.Process.Id))
                    {
                        continue;
                    }
                    using (EventLog eventLog = new EventLog("Application"))
                    {
                        eventLog.Source = this.ServiceName;
                        eventLog.WriteEntry($"开始守护进程,命令:${protectProgress.Command}", EventLogEntryType.Information, 101, 1);
                    }                    
                    try
                    {
                        protectProgress.Process = startCmd(protectProgress.Command);
                        if (protectProgress.Process != null)
                        {
                            protectProgress.ProtectCount++;
                            using (EventLog eventLog = new EventLog("Application"))
                            {
                                eventLog.Source = this.ServiceName;
                                eventLog.WriteEntry($"命令:${protectProgress.Command}守护成功,当前第${protectProgress.ProtectCount}次. ID:${protectProgress.Process.ProcessName} NAME: ${protectProgress.Process.Id}", EventLogEntryType.Information,200, 1);
                            }                            
                            protectProgress.Name = protectProgress.Process.ProcessName;
                        }
                    }
                    catch (Exception ex)
                    {
                        using (EventLog eventLog = new EventLog("Application"))
                        {
                            eventLog.Source = this.ServiceName;                            
                            eventLog.WriteEntry($"命令:${protectProgress.Command}守护失败,${ex.Message}", EventLogEntryType.Error, 500, 1);
                        }                        
                    }
                }
                
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }


        private Process startCmd(string cmd)
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
        public string Name { get; set; }
        //public bool IsStarted { get; set; }
        public int ProtectCount { get; set; }
        public Process Process { get; set; }
    }
}
