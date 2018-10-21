using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Hangman.Instance.NamedPipe.Interfaces;
using Hangman.Instance.NamedPipe.Utilities;

namespace Hangman.Instance.NamedPipe.Client
{
    public class PipeClient : ICommunicationClient
    {
        #region private fields

        private readonly NamedPipeClientStream _pipeClient;

        #endregion

        #region c'tor

        public PipeClient(string serverId)
        {
            _pipeClient = new NamedPipeClientStream(".", serverId, PipeDirection.InOut, PipeOptions.Asynchronous);
        }

        #endregion

        #region ICommunicationClient implementation

        /// <summary>
        /// Starts the client. Connects to the server.
        /// </summary>
        public void Start()
        {
            const int tryConnectTimeout = 5*60*1000; // 5 minutes
            _pipeClient.Connect(tryConnectTimeout);
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

        public Task<TaskResult> SendMessage(string message)
        {
            var taskCompletionSource = new TaskCompletionSource<TaskResult>();

            if (_pipeClient.IsConnected)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                _pipeClient.BeginWrite(buffer, 0, buffer.Length, asyncResult =>
                {
                    try
                    {
                        taskCompletionSource.SetResult(EndWriteCallBack(asyncResult));
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.SetException(ex);
                    }

                }, null);
            }
            else
            {
                throw new IOException("pipe is not connected");
            }

            return taskCompletionSource.Task;
        }
        
        public Task<string> Receive()
        {
            var taskCompletionSource = new TaskCompletionSource<string>();
            if (_pipeClient.IsConnected)
            {
                var buffer = new byte[4096];
                _pipeClient.BeginRead(buffer, 0, 4096, asyncResult =>
                {
                    try
                    {
                        _pipeClient.EndRead(asyncResult);
                        _pipeClient.Flush();
                        taskCompletionSource.SetResult(Encoding.UTF8.GetString(buffer).Trim('\0', ' ', '\t', '\n'));
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

        #region private methods

        /// <summary>
        /// This callback is called when the BeginWrite operation is completed.
        /// It can be called whether the connection is valid or not.
        /// </summary>
        /// <param name="asyncResult"></param>
        private TaskResult EndWriteCallBack(IAsyncResult asyncResult)
        {
            _pipeClient.EndWrite(asyncResult);
            _pipeClient.Flush();

            return new TaskResult {IsSuccess = true};
        }

        #endregion
    }
}
