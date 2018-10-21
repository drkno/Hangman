using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hangman.Instance.NamedPipe.Client;
using Hangman.Instance.NamedPipe.Interfaces;
using Hangman.Instance.NamedPipe.Server;

namespace Hangman.Instance
{
    public abstract class ProgramInstance
    {
        private static readonly string ProgramName = Process.GetCurrentProcess().ProcessName;
        private static readonly Mutex RuntimeMutex = new Mutex(true, ProgramName);
        private static PipeServer _pipeServer;
        private static readonly ProgramInstance ProgInstance = new Program();

        protected abstract void RunMain(string[] args, string workingDirectory, IDictionary<string, string> environment);
        protected abstract void NewInstance(string[] args, string workingDirectory, IDictionary<string, string> environment, TextWriter console);

        private static void GetEnvironmentData(out string workingDir, out Dictionary<string, string> envVars)
        {
            workingDir = Environment.CurrentDirectory;
            envVars = new Dictionary<string, string>();
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                envVars.Add(key, Environment.GetEnvironmentVariable(key));
            }
        }

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
            ProgInstance.NewInstance(cliArguments, workingDir, dic, e.Writer);
            e.Flush();
        }

        private static void MainProcess(string[] args)
        {
            _pipeServer = new PipeServer(ProgramName);
            _pipeServer.MessageReceivedEvent += ExtractAndPushInstance;
            _pipeServer.Start();
            GetEnvironmentData(out string workingDir, out Dictionary<string, string> envVars);
            ProgInstance.RunMain(args, workingDir, envVars);
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
            Task.Run(async() =>
            {
                var message = $"{string.Join("\0", args)}\0\0{Environment.CurrentDirectory}\0\0{string.Join("\0", str)}";
                await pipeClient.SendMessage(message);
                var response = await pipeClient.Receive();
                Console.Write(response);
            }).Wait();
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
