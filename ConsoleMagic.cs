using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BranchReload2
{
    public class ConsoleMagic
    {
        
        public const string COMPLETED = nameof(COMPLETED);

        private AsinkOverseer Overseer { get; set; }
        
        public ConsoleMagic(AsinkOverseer overseer)
        {
            Overseer = overseer;
        }

        public ConcurrentQueue<(string command, List<string> queue)> CommandQueue = new ();
        
        public (string command, List<string> queue) AddCommand(string args)
        {
            (string command, List<string> queue) hold = (args, new ());
            CommandQueue.Enqueue(hold);
            return hold;
        }
        
        public async Task LaunchAndDestroy()
        {
            Console.WriteLine("starting");
            while (true)
            {
                if (CommandQueue.TryDequeue(out (string command, List<string> queue) item))
                {
                    CreateConsole(item);
                }
                await Task.Delay(10);
            }
        }

        public Process CreateCMDInstance()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "cmd";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            Process commandProc = new Process();
            commandProc.StartInfo = startInfo;
            commandProc.Start();
            return commandProc;
        }
                
        public void CreateConsole((string args, List<string> queue) item)
        {
            Process commandProc = CreateCMDInstance();
            
            ConcurrentQueue<string> itemsOut = new();
            ConcurrentQueue<string> itemsErr = new();
            
            commandProc.OutputDataReceived += (a, b) =>
            {
                //Console.WriteLine($"out >> {b.Data}");
                if (b.Data is null) return;
                itemsOut.Enqueue(b.Data);
            };
            
            commandProc.ErrorDataReceived += (a, b) =>
            {
                //Console.WriteLine($"err >> {b.Data}");
                if (b.Data is null) return;
                itemsErr.Enqueue(b.Data);
            };
            
            commandProc.StandardInput.WriteLine($"{item.args} &exit");
            
            commandProc.BeginOutputReadLine();
            commandProc.BeginErrorReadLine();

            Console.WriteLine($"command > {item.args}");
            Overseer.YieldUntil(() => commandProc.HasExited).WaitOne();
            
            itemsOut.ToList().ForEach(e=>item.queue.Add(e));
            itemsErr.ToList().ForEach(e=>item.queue.Add(e));
            
            item.queue.Add(COMPLETED);
        }
    }
}