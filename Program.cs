using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BranchReload2
{
    public class Builderino
    {

        public GitFileConfigs GitFileConfigs { get; set; }
        
        public List<Action> CheckYieldBag { get; set; } = new ();

        public ManualResetEvent YieldUntil(Expression<Func<bool>> expression)
        {
            
            var reset = new ManualResetEvent(false);
            void Item()
            {
                if (expression.Compile().Invoke())
                {
                    lock (CheckYieldBag) CheckYieldBag.Remove(Item);
                    reset.Set();
                }
            }

            lock (CheckYieldBag) CheckYieldBag.Add(Item);

            return reset;
        }

        public async Task CheckYields()
        {
            int i = 0;
            try
            {
                while (true)
                {
                    await Task.Delay(10);
                    int count = 0;
                    Action action;
                    
                    lock (CheckYieldBag)
                    {
                        action = CheckYieldBag.ElementAtOrDefault(i);
                        count = CheckYieldBag.Count;
                    }

                    action?.Invoke();

                    
                    i++;
                    if (count == 0)
                        i = 0;
                    else
                        i %= count;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void WaitUntil(Expression<Func<bool>> expression) => YieldUntil(expression).WaitOne();
        
        public Builderino()
        {
            CheckYields();
            
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

        public const string COMPLETED = nameof(COMPLETED);

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
                itemsOut.Enqueue(b.Data);
            };
            
            commandProc.ErrorDataReceived += (a, b) =>
            {
                //Console.WriteLine($"err >> {b.Data}");
                itemsErr.Enqueue(b.Data);
            };
            
            commandProc.StandardInput.WriteLine($"{item.args} &exit");
            
            commandProc.BeginOutputReadLine();
            commandProc.BeginErrorReadLine();

            Console.WriteLine($"command > {item.args}");
            YieldUntil(() => commandProc.HasExited).WaitOne();
            
            itemsOut.ToList().ForEach(e=>item.queue.Enqueue(e));
            itemsErr.ToList().ForEach(e=>item.queue.Enqueue(e));
            item.queue.Enqueue(COMPLETED);
        }

        public IEnumerable<string> DequeueEach(ConcurrentQueue<string> items)
        {
            while (true)
            {
                YieldUntil(() => !items.IsEmpty).WaitOne();
                items.TryDequeue(out string response);
                if (response is COMPLETED)
                    yield break;
                yield return response;
            }
        }

        public bool AddGitInitIfNotExists()
        {
            if (Directory.Exists("./.git")) return false;
            var (command, queue) = AddCommand("git init .");
            return true;
        }

        public string GetCurrentBranch((string command, ConcurrentQueue<string> queue) command) => 
            DequeueEach(command.queue)
                .SingleOrDefault(e => e is not null && !e.Equals(
                    e.Replace("On branch ", "")))?.Split(" ")[2]??null;
        
        public void PerformGitCommands()
        {
            string stashGuid = Guid.NewGuid().ToString().Replace("-", "_");

            string remoteName = "hotreload";

            bool freshGit = AddGitInitIfNotExists();
            
            //AddCommand($"echo {GitFileConfigs.BranchId} > {new Random().Next(10000000, 1000000000)}.txt");

            string currentBranch = GetCurrentBranch(AddCommand($"git status"));
            
            if(currentBranch is null) throw new Exception("No branch to track");
            Console.WriteLine($"Branch > {currentBranch}");

            if (freshGit)
            {
                //wait to execute this because if the git file doesn't exist, this might break something.
                AddCommand($"git add .");
                //AddCommand($"git commit -m initial_commit");
            }

            AddCommand($"git stash push -u -m stash_{stashGuid}");
            AddCommand($"git stash apply --index");
            AddCommand($"git remote add {remoteName} {GitFileConfigs.RepoUrl()}");
            //AddCommand($"git push {remoteName} --delete hotreload_{branchHash}");
            //AddCommand($"git checkout --orphan hotreload_{branchHash}");
            AddCommand($"git checkout -b hotreload_{GitFileConfigs.BranchId}");
            AddCommand($"git fetch");
            AddCommand($"git pull");
            AddCommand($"git add .");
            AddCommand($"git commit -m \"Pushed on: {DateTime.UtcNow.ToString()}\"");
            // AddCommand($"git push {remoteName} hotreload_{branchHash}");
            var gitPushHold = DequeueEach(AddCommand($"git push -f -u git@github.com:{GitFileConfigs.UserName}/{GitFileConfigs.RepoName}.git hotreload_{GitFileConfigs.BranchId}").queue);
            AddCommand($"git checkout {currentBranch}");
            AddCommand($"git remote remove {remoteName}");
            AddCommand($"git branch -D hotreload_{GitFileConfigs.BranchId}");
            
            var gitStashApplyHold = DequeueEach(AddCommand($"git stash apply --index").queue);
            
            YieldUntil(() => CommandQueue.IsEmpty).WaitOne();
            Console.WriteLine(string.Join("out >> ",gitPushHold.Select(e => $"{e}\n")));
            Console.WriteLine(string.Join("out >> ",gitStashApplyHold.Select(e => $"{e}\n")));
            Console.WriteLine(COMPLETED);
        }

        public void SetupConfigItems()
        {
            string file_name = "./externalbranch_config.json";
            if (!File.Exists(file_name))
            {
                GitFileConfigs GFC = new(){Key = ""};
                //if the file doesn't exist, it will to set to a blank JSON of the GitFileConfigs instance.
                File.WriteAllText(file_name, JsonConvert.SerializeObject(GFC));
            }
            
            GitFileConfigs RetrievedConfigs = JsonConvert.DeserializeObject<GitFileConfigs>(File.ReadAllText(file_name));

            if (RetrievedConfigs.EmptyValue(RetrievedConfigs.BranchId))
            {
                RetrievedConfigs.BranchId = Guid.NewGuid().ToString().Replace("-","_");
            }
            
            
            if (RetrievedConfigs.KeyHashIsValid())
            {
                Console.WriteLine($"Existing Key value {RetrievedConfigs.Key} was generated by this program (or one using the same method to setup the string)");
            }
            
            if (HasValidConfigContent(RetrievedConfigs))
            {
                Console.WriteLine($"Existing key manually entered: {RetrievedConfigs.Key} {RetrievedConfigs.GetHash()}");
                if (!RetrievedConfigs.KeyHashIsValid())
                {
                    RetrievedConfigs.Key = RetrievedConfigs.GetHash();
                    SetAccessTokenAsEnvironmentVariable(RetrievedConfigs.Key);
                }
                File.WriteAllText(file_name, JsonConvert.SerializeObject(RetrievedConfigs));
            }
            else
            {
                throw new ("Config file has missing entries.");
            }

            GitFileConfigs = RetrievedConfigs;
        }

        public bool HasValidConfigContent(GitFileConfigs configs)
        {
            return configs.HasValidEntries() && !configs.KeyIsUnset();
        }

        public void SetAccessTokenAsEnvironmentVariable(string access_key)
        {
            Console.WriteLine(">>"+Environment.GetEnvironmentVariable(EnvironmentWriter.AccessTokenTarget, EnvironmentVariableTarget.User));
            Environment.SetEnvironmentVariable(EnvironmentWriter.AccessTokenTarget, access_key, EnvironmentVariableTarget.User);
        }
    }

    public class EnvironmentWriter
    {
        public const string AccessTokenTarget = "BranchReloadAccessToken";
    }
    
    public class GitFileConfigs
    {
        public string UserName { get; set; }
        public string RepoName { get; set; }
        public string BranchId { get; set; }
        
        //public string AccessToken { get; set; }
        public string Key { get; set; }
        
        public const string UNSET_KEY = nameof(UNSET_KEY);

        private string _RepoUrl { get; set; }

        public string RepoUrl() => _RepoUrl ??= 
            $"https://{Environment.GetEnvironmentVariable(EnvironmentWriter.AccessTokenTarget, EnvironmentVariableTarget.User)}@github.com/{UserName}/{RepoName}.git";

        public bool EmptyValue(string val)
        {
            return string.IsNullOrEmpty(val) || string.IsNullOrWhiteSpace(val);
        }
        
        public bool HasValidEntries()
        {
            if (EmptyValue(UserName))
                return false;
            
            if (EmptyValue(RepoName))
                return false;

            return true;
        }

        /// <summary>
        /// If the key is empty, or unset, that means the user is expected to enter it in.
        /// The key is replaced by a hash when the program detects an access_token.
        /// </summary>
        public bool KeyIsUnset() => EmptyValue(Key) || Key is UNSET_KEY;

        public bool KeyHashIsValid()
        {
            if (Key.Contains(":"))
            {
                string[] parts = Key.Split(":");
                int step1 = GetValueOfString("abcd" + parts[0]);
                Console.WriteLine(step1+":"+parts[1]);
                return (step1+"").Equals(parts[1]);
            }
            return false;
        }

        public int GetValueOfString(string s)
        {
            int i = 0;
            foreach (var c in s) i += (byte) c;
            return i;
        }
        
        public string GetHash()
        {
            int partA = GetValueOfString(Key);
            int partB = GetValueOfString("abcd" + partA);
            return $"{partA}:{partB}";
        }
    }
    
    class Program
    {
        static async Task Main(string[] args)
        {
            var hold = new Builderino();

            hold.SetupConfigItems();
            
            hold.LaunchAndDestroy();
            
            hold.PerformGitCommands();
        }
    }
}