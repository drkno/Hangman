using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Hangman.Instance.NamedPipe.Interfaces;

namespace Hangman.Instance.NamedPipe.Client
{
    public class PipeClient : ICommunication
    {
        #region private fields

        private readonly NamedPipeClientStream _pipeClient;
        public TextReader Reader { get; }
        public TextWriter Writer { get; }

        #endregion

        #region c'tor

        public PipeClient(string serverId)
        {
            _pipeClient = new NamedPipeClientStream(".", serverId, PipeDirection.InOut, PipeOptions.Asynchronous);
            Reader = new StreamReader(_pipeClient);
            Writer = new StreamWriter(_pipeClient);
        }

        #endregion

        #region ICommunicationClient implementation

        /// <summary>
        /// Starts the client. Connects to the server.
        /// </summary>
        public void Start()
        {
            _pipeClient.Connect((int) TimeSpan.FromMinutes(5).TotalMilliseconds);
        }

        /// <summary>
        /// Stops the client. Waits for pipe drain, closes and disposes it.
        /// </summary>
        public void Stop()
        {
            try
            {
                _pipeClient.WaitForPipeDrain();
            }
            finally
            {
                _pipeClient.Close();
                _pipeClient.Dispose();
            }
        }

        public Task SendMessage(string message)
        {
            return Writer.WriteAsync(message);
        }
        
        public Task<string> Receive()
        {
            var taskCompletionSource = new TaskCompletionSource<string>();
            if (_pipeClient.IsConnected)
            {
                var buffer = new byte[1];
                _pipeClient.BeginRead(buffer, 0, 1, asyncResult =>
                {
                    try
                    {
                        _pipeClient.EndRead(asyncResult);
                        _pipeClient.Flush();
                        taskCompletionSource.SetResult(Encoding.UTF8.GetString(buffer) + Reader.ReadToEnd());
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.SetException(ex);
                    }
                }, taskCompletionSource);
            }
            else
            {
                throw new IOException("pipe is not connected");
            }
            return taskCompletionSource.Task;
        }

        #endregion
    }
}
