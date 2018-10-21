using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Hangman.Queue
{
    public class Command
    {
        private readonly string[] _args;
        private readonly string _workingDirectory;
        private readonly IDictionary<string, string> _environmentVariables;
        private Process _process;

        public Command(string[] args, string workingDirectory, IDictionary<string, string> environmentVariables)
        {
            _args = args;
            _workingDirectory = workingDirectory;
            _environmentVariables = environmentVariables;
        }

        public event EventHandler OnExecutionComplete;

        public string Status
        {
            get
            {
                if (_process == null)
                {
                    return "Queued";
                }
                if (!_process.HasExited)
                {
                    return "Executing";
                }
                return "Complete";
            }
        }

        public void BeginExecute()
        {
            var startInfo = new ProcessStartInfo();
            foreach (var key in _environmentVariables.Keys)
            {
                startInfo.EnvironmentVariables[key] = _environmentVariables[key];
            }
            startInfo.UseShellExecute = false;
            startInfo.FileName = _args[0];
            startInfo.WorkingDirectory = _workingDirectory;
            startInfo.Arguments = string.Join(" ", _args.Skip(1).Select(s => s.Contains(" ") ? $"\"{s}\"" : s));
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;

            _process = Process.Start(startInfo);
            if (_process != null)
            {
                _process.EnableRaisingEvents = true;
                _process.OutputDataReceived += _process_OutputDataReceived;
                _process.ErrorDataReceived += _process_ErrorDataReceived;
                _process.Exited += _process_Exited;
            }
            else
            {
                throw new Exception("Failed to start process.");
            }
        }

        private static void _process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static void _process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private void _process_Exited(object sender, EventArgs e)
        {
            OnExecutionComplete?.Invoke(this, null);
        }

        public override string ToString()
        {
            return string.Join(" ", _args.Select(s => s.Contains(" ") ? $"\"{s}\"" : s));
        }

        public void Kill()
        {
            _process.Exited -= _process_Exited;
            _process.Kill();
        }
    }
}
