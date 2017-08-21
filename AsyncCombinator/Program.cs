using System;
using System.Collections.Generic;
using System.Linq;
using AsyncCombinator.Instance;

namespace AsyncCombinator
{
    public class Program : ProgramInstance
    {
        private string defaultCommand;

        protected override void RunMain(string[] args)
        {
            PreventExit();
        }

        protected override void NewInstance(string[] args, string workingDirectory, IDictionary<string, string> environment)
        {
            Print(args, workingDirectory, environment);
        }

        private void Print(string[] args, string workingDirectory, IDictionary<string, string> environmentVariables)
        {
            var command = new 

            Console.WriteLine("New Instance");
            Console.WriteLine("ARGS=" + string.Join(" ", args));
            Console.WriteLine("PWD=" + workingDirectory);
            Console.WriteLine("VARS=\n\t" + string.Join("\n\t", environmentVariables.Keys.Select(k => k + " = " + environmentVariables[k])));
            Console.WriteLine();
        }
    }
}
