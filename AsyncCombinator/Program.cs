using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using AsyncCombinator.Instance;
using AsyncCombinator.Queue;

namespace AsyncCombinator
{
    public class Program : ProgramInstance
    {
        private ExecutorQueue<Command> _commandQueue;

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public static string WhereSearch(string filename)
        {
            var paths = new[] { Environment.CurrentDirectory }.Concat(Environment.GetEnvironmentVariable("PATH").Split(';'));
            string[] extensions = {""};
            try
            {
                extensions = extensions.Concat(Environment.GetEnvironmentVariable("PATHEXT").Split(';').Where(e => e.StartsWith("."))).ToArray();
            }
            catch
            {
                // ignored - don't care because in this case everything is already setup to succeed
            }
            var combinations = paths.SelectMany(x => extensions, (path, extension) => Path.Combine(path, filename + extension));
            return combinations.FirstOrDefault(File.Exists);
        }

        private void List(string _)
        {
            var ids = new List<string>();
            var status = new List<string>();
            var jobs = new List<string>();
            foreach (var ip in _commandQueue)
            {
                ids.Add(ip.QueueId.ToString());
                status.Add(ip.Item.Status);
                jobs.Add(ip.Item.ToString());
            }

            var idWid = Math.Max(ids.Count > 0 ? ids.Max(id => id.Length) + 1 : 20, 20);
            var statWid = Math.Max(status.Count > 0 ? status.Max(stat => stat.Length) + 1 : 20, 20);
            var jobWid = Math.Max(jobs.Count > 0 ? jobs.Max(job => job.Length) + 1 : 20, 20);

            Console.WriteLine("Id".PadRight(idWid, ' ') + "Status".PadRight(statWid, ' ') + "Job".PadRight(jobWid, ' '));
            Console.WriteLine(new string('-', idWid + jobWid + statWid));
            for (var i = 0; i < ids.Count; i++)
            {
                Console.WriteLine(ids[i].PadRight(idWid, ' ') + status[i].PadRight(statWid, ' ') + jobs[i].PadRight(jobWid, ' '));
            }
        }

        private void ProcessCliArguments(ref string[] args)
        {
            var i = 0;
            for (; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-") && WhereSearch(args[i]) != null)
                {
                    break;
                }
            }
            var cliArgs = args.Take(i);
            var cliOptions = new OptionSet
            {
                {"c|clear", "Clear the current work backlog.", _ => _commandQueue.ClearBacklog()},
                {"k|kill", "Kill a currently in-progress item. This may have side affects.", id => _commandQueue.MarkComplete(BigInteger.Parse(id)).Item.Kill()},
                {"l|list", "List all backlog and in-progress jobs.", List},
                {"m|max", "Set the maximum number of parallel jobs", max => _commandQueue.MaximumParallelExecutions = int.Parse(max)}
            };

            try
            {
                cliOptions.ParseExceptionally(cliArgs);
            }
            catch
            {
                cliOptions.ShowHelp(Process.GetCurrentProcess().ProcessName, "A parallel command executor which does not block", "", "", "Matthew Knox", "https://github.com/mrkno/Hangman/issues", "Copyright " + DateTime.Now.Year + " (c) Matthew Knox", false);
                args = new string[] { };
                return;
            }

            args = args.Skip(i).Take(args.Length - i + 1).ToArray();
        }

        protected override void RunMain(string[] args)
        {
            _commandQueue = new ExecutorQueue<Command>();
            _commandQueue.ShouldExecute += CommandShouldExecute;

            ProcessCliArguments(ref args);
            if (args.Length > 0)
            {
                throw new Exception("Did not handle all CLI arguments.");
            }
            PreventExit();
        }

        private void CommandShouldExecute(object sender, ExecutorQueue<Command>.ExecutorQueueItem e)
        {
            e.Item.OnExecutionComplete += (s, v) => e.MarkComplete();
            e.Item.BeginExecute();
        }

        protected override void NewInstance(string[] args, string workingDirectory, IDictionary<string, string> environment)
        {
            ProcessCliArguments(ref args);
            if (args.Length > 0)
            {
                _commandQueue.Enqueue(new Command(args, workingDirectory, environment));
            }
        }
    }
}
