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
            things.AddSingleton<AsinkOverseer>();
            things.AddSingleton<DoGitConfigLoad>();
            things.AddSingleton<Builderino>();
            things.AddSingleton<ConsoleMagic>();
            things.AddSingleton<DoConsoleInstructions>();
            
            var provider = things.BuildServiceProvider();

            var hold = provider.GetService<Builderino>();  
            
            hold.ConsoleMagic.LaunchAndDestroy();
            
            provider.GetService<DoConsoleInstructions>().PerformGitCommands();
            
            //ping github action
        }
    }
}