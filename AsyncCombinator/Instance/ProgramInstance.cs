using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AsyncCombinator.Instance.NamedPipe.Client;
using AsyncCombinator.Instance.NamedPipe.Interfaces;
using AsyncCombinator.Instance.NamedPipe.Server;

namespace AsyncCombinator.Instance
{
    public abstract class ProgramInstance
    {
        private static readonly string ProgramName = Process.GetCurrentProcess().ProcessName;
        private static readonly Mutex RuntimeMutex = new Mutex(true, ProgramName);
        private static PipeServer _pipeServer;
        private static readonly ProgramInstance ProgInstance = new Program();

        protected abstract void RunMain(string[] args);
        protected abstract void NewInstance(string[] args, string workingDirectory, IDictionary<string, string> environment);

        private static void ExtractAndPushInstance(object sender, MessageReceivedEventArgs e)
        {
            var sections = e.Message.Split(new[] {"\0\0"}, StringSplitOptions.None);
            if (sections.Length != 3)
            {
                return;
            }

            var cliArguments = sections[0].Split('\0');
            var workingDir = sections[1];

            var envVars = sections[2].Split('\0');
            var dic = new Dictionary<string, string>();
            for (var i = 0; i < envVars.Length; i += 2)
            {
                dic[envVars[i]] = envVars[i + 1];
            }
            ProgInstance.NewInstance(cliArguments, workingDir, dic);
        }

        private static void MainProcess(string[] args)
        {
            _pipeServer = new PipeServer(ProgramName);
            _pipeServer.MessageReceivedEvent += ExtractAndPushInstance;
            _pipeServer.Start();
            ProgInstance.RunMain(args);
        }

        public static void ExitProcess()
        {
            _pipeServer.Stop();
            Environment.Exit(0);
        }

        public static void PreventExit()
        {
            Process.GetCurrentProcess().WaitForExit();
        }

        private static void SecondaryProcess(string[] args)
        {
            var pipeClient = new PipeClient(ProgramName);
            pipeClient.Start();
            var str = new List<string>();
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                str.Add(key);
                str.Add(Environment.GetEnvironmentVariable(key));
            }
            Task.Run(() => pipeClient.SendMessage($"{string.Join("\0", args)}\0\0{Environment.CurrentDirectory}\0\0{string.Join("\0", str)}")).Wait();
            pipeClient.Stop();
        }

        public static void Main(string[] args)
        {
            if (RuntimeMutex.WaitOne(TimeSpan.Zero, true))
            {
                MainProcess(args);
            }
            else
            {
                SecondaryProcess(args);
            }
        }
    }
}
