using System;
using System.Collections.Concurrent;
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

        public ConcurrentQueue<(string command, ConcurrentQueue<string> queue)> CommandQueue = new ();
        
        public (string command, ConcurrentQueue<string> queue) AddCommand(string args)
        {
            var hold = (args, new ConcurrentQueue<string>());
            CommandQueue.Enqueue(hold);
            return hold;
        }
        
        public async Task LaunchAndDestroy()
        {
            Console.WriteLine("starting");
            while (true)
            {
                if (CommandQueue.TryDequeue(out (string command, ConcurrentQueue<string> queue) item))
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
                
        public void CreateConsole((string args, ConcurrentQueue<string> queue) item)
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
            
            itemsOut.ToList().ForEach(e=>item.queue.Enqueue(e));
            itemsErr.ToList().ForEach(e=>item.queue.Enqueue(e));
            
            item.queue.Enqueue(COMPLETED);
        }
    }
}