using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BranchReload2
{
    class Program
    {
        static async Task Main(string[] args)
        {

            ServiceCollection things = new ServiceCollection();

            var overseer = new AsinkOverseer();
            var magic = new ConsoleMagic(overseer);
            var hold = new Builderino(overseer, magic);
            var instructions = new DoConsoleInstructions(overseer, magic, new DoGitConfigLoad());
            
            hold.ConsoleMagic.LaunchAndDestroy();
            
            instructions.PerformGitCommands();
            
            //ping github action
        }
    }
}