using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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

        protected abstract void RunMain(string[] args);
        protected abstract string NewInstance(string[] args, string workingDirectory, IDictionary<string, string> environment);

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
            var res = ProgInstance.NewInstance(cliArguments, workingDir, dic);
            var ress = Encoding.UTF8.GetBytes(res);
            var buff = new byte[4096];
            for (var i = 0; i < ress.Length; i++)
            {
                buff[i] = ress[i];
            }
            for (var i = ress.Length; i < 4096; i++)
            {
                buff[i] = 0x0;
            }
            e.SendMessage(buff);
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
            Task.Run(async() =>
            {
                var message = $"{string.Join("\0", args)}\0\0{Environment.CurrentDirectory}\0\0{string.Join("\0", str)}";
                await pipeClient.SendMessage(message);
                var response = await pipeClient.Receive();
                Console.WriteLine(response);
            }).Wait();
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
