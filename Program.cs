using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace BranchReload2
{

    public class Builderino
    {
        string branchHash = "e478b504_989a_4f0b_85ae_c261e6ba989a";//Guid.NewGuid().ToString().Replace("-","_");
        string repo = "TestVueExample4";
        string key = "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";
        string user = "LandanParker";
        string repoUrl;

        public Builderino()
        {
            repoUrl = $"https://{key}@github.com/{user}/{repo}.git";
        }

        public ConcurrentQueue<(string command, ConcurrentQueue<string> queue)> CommandQueue = new ();

        public (string command, ConcurrentQueue<string> queue) AddCommand(string args)
        {
            var hold = (args, new ConcurrentQueue<string>());
            CommandQueue.Enqueue(hold);
            return hold;
        }
        
        public void LaunchAndDestroy()
        {
            Task.Delay(0).ContinueWith(async e=>
            {
                Console.WriteLine("starting");
                while (true)
                {
                    if (CommandQueue.TryDequeue(out (string command, ConcurrentQueue<string> queue) item))
                    {
                        await CreateConsole(item);
                    }
                    await Task.Delay(10);
                }
            });
            
        }

        public const string COMPLETED = nameof(COMPLETED);

        public async Task CreateConsole((string args, ConcurrentQueue<string> queue) item)
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

            ConcurrentQueue<string> itemsOut = new();
            ConcurrentQueue<string> itemsErr = new();
            
            commandProc.OutputDataReceived += (a, b) =>
            {
                //Console.WriteLine($"out >> {b.Data}");
                itemsOut.Enqueue(b.Data);
            };
            
            commandProc.ErrorDataReceived += (a, b) =>
            {
                //Console.WriteLine($"err >> {b.Data}");
                itemsErr.Enqueue(b.Data);
            };
            
            await commandProc.StandardInput.WriteLineAsync($"{item.args} &exit");
            
            commandProc.BeginOutputReadLine();
            commandProc.BeginErrorReadLine();
            

            ManualResetEvent mre = new (false);
            Task.Run(async () =>
            {
                while (!commandProc.HasExited)
                {
                    await Task.Delay(10);
                }
                itemsOut.ToList().ForEach(e=>item.queue.Enqueue(e));
                itemsErr.ToList().ForEach(e=>item.queue.Enqueue(e));
                item.queue.Enqueue(COMPLETED);
                mre.Set();
            });
            mre.WaitOne();
        }

        public IEnumerable<string> DequeueEach(ConcurrentQueue<string> items)
        {

            while (true)
            {
                ClaspLockCondition(() => items.IsEmpty, async () => { await Task.Delay(50); }, false, out ManualResetEvent mre);
                mre.WaitOne();
                items.TryDequeue(out string response);
                if (response is COMPLETED)
                    yield break;
                yield return response;
            }
            
            while (true)
            {
                ManualResetEvent mre = new(false);
                Task.Run(async () =>
                {
                    while (items.IsEmpty)
                    {
                        await Task.Delay(50);
                    }
                    mre.Set();
                });
                mre.WaitOne();
                
                items.TryDequeue(out string response);
                if (response is COMPLETED)
                    yield break;
                yield return response;
            }
        }

        public void ClaspLockCondition(Expression<Func<bool>> conditions, Action action, bool start, out ManualResetEvent reset)
        {
            reset = new ManualResetEvent(start);
            while (conditions.Compile().Invoke())
            {
                action();
            }
            reset.Set();
        }

        public void Perform()
        {
            string stashGuid = Guid.NewGuid().ToString().Replace("-", "_");

            bool freshGit = false;

            if (!Directory.Exists("./.git"))
            {
                freshGit = true;
                var (command, queue) = AddCommand("git init .");
            }

            //AddCommand($"echo {branchHash} > {new Random().Next(10000000, 1000000000)}.txt");

            var currentBranch = DequeueEach(AddCommand($"git status").queue)
                .SingleOrDefault(e => e is not null && !e.Equals(e.Replace("On branch ", "")))?.Split(" ")[2]??null;

            if(currentBranch is null) throw new Exception("No branch to track");

            Console.WriteLine($"Branch > {currentBranch}");

            if (freshGit)
            {
                AddCommand($"git add .");
                AddCommand($"git commit -m initial_commit");
            }

            AddCommand($"git stash push -u -m stash_{stashGuid}");
            AddCommand($"git stash apply --index");

            branchHash = "f3a73bc6_9940_47a9_bf5s9_19e638a99547e";
            string remoteName = "hotreload";
            
            AddCommand($"git remote add {remoteName} {repoUrl}");
            //AddCommand($"git push {remoteName} --delete hotreload_{branchHash}");
            // AddCommand($"git checkout --orphan hotreload_{branchHash}");
            AddCommand($"git checkout -b hotreload_{branchHash}");
            AddCommand($"git add .");
            AddCommand($"git commit -m test");
            AddCommand($"git push -f {remoteName} hotreload_{branchHash}");
            AddCommand($"git checkout {currentBranch}");
            AddCommand($"git remote remove {remoteName}");
            AddCommand($"git branch -D hotreload_{branchHash}");
            AddCommand($"git stash apply --index");

            ManualResetEvent mre = new (false);
            
            Task.Run(async ()=>
            {
                while (!CommandQueue.IsEmpty) await Task.Delay(50);
                mre.Set();
            });
            
            mre.WaitOne();
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            var hold = new Builderino();
            ManualResetEvent mre = new ManualResetEvent(false);
            hold.LaunchAndDestroy();
            
            hold.Perform();
        }
    }
}