﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace BranchReload2
{
    public class DoConsoleInstructions
    {
        
        public GitFileConfigs GitFileConfigs { get; set; }
        public ConsoleMagic ConsoleMagic { get; set; }

        private AsinkOverseer Overseer { get; set; }
        
        public DoConsoleInstructions(AsinkOverseer overseer, ConsoleMagic consoleMagic, DoGitConfigLoad configLoad)
        {
            Overseer = overseer;
            ConsoleMagic = consoleMagic;
            configLoad.SetupConfigItems();
            GitFileConfigs = configLoad.GitFileConfigs;
        }
        
        public Func<string, (string command, ConcurrentQueue<string> queue)> AddCommand => ConsoleMagic.AddCommand;
        
        public bool AddGitInitIfNotExists()
        {
            if (Directory.Exists("./.git")) return false;
            var (command, queue) = ConsoleMagic.AddCommand("git init .");
            return true;
        }

        public string GetCurrentBranch((string command, ConcurrentQueue<string> queue) command) => 
            Overseer.DequeueEach(command.queue).SingleOrDefault(e => e is not null && !e.Equals(e.Replace("On branch ", "")))?.Split(" ")[2]??null;
        
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
            //AddCommand($"git checkout --orphan  hotreload_{GitFileConfigs.BranchId}");
            AddCommand($"git checkout -b hotreload_{GitFileConfigs.BranchId}");
            AddCommand($"git fetch");
            AddCommand($"git pull");
            AddCommand($"git add .");
            AddCommand($"git commit -m \"Pushed on: {DateTime.UtcNow.ToString()}\"");
            // AddCommand($"git push {remoteName} hotreload_{branchHash}");
            var gitPushHold = Overseer.DequeueEach(AddCommand($"git push -f -u git@github.com:{GitFileConfigs.UserName}/{GitFileConfigs.RepoName}.git hotreload_{GitFileConfigs.BranchId}").queue);
            AddCommand($"git checkout {currentBranch}");
            AddCommand($"git remote remove {remoteName}");
            AddCommand($"git branch -D hotreload_{GitFileConfigs.BranchId}");
            
            var gitStashApplyHold = Overseer.DequeueEach(AddCommand($"git stash apply --index").queue);
            
            Overseer.YieldUntil(() => ConsoleMagic.CommandQueue.IsEmpty).WaitOne();
            Console.WriteLine(string.Join("out >> ",gitPushHold.Select(e => $"{e}\n")));
            Console.WriteLine(string.Join("out >> ",gitStashApplyHold.Select(e => $"{e}\n")));
            Console.WriteLine(ConsoleMagic.COMPLETED);
        }
    }
}