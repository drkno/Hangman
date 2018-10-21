using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Hangman.Instance.NamedPipe.Interfaces;

namespace Hangman.Instance.NamedPipe.Server
{
    internal class InternalPipeServer : ICommunicationServer, IDisposable
    {
        #region private fields

        private readonly NamedPipeServerStream _pipeServer;
        private bool _isStopping;
        private readonly object _lockingObject = new object();
        private const int BufferSize = 2048;
        public readonly string Id;
        public TextWriter Writer { get; }

        private class Info
        {
            public readonly byte[] Buffer;
            public readonly StringBuilder StringBuilder;

            public Info()
            {
                Buffer = new byte[BufferSize];
                StringBuilder = new StringBuilder();
            }
        }

        #endregion

        #region c'tor

        /// <summary>
        /// Creates a new NamedPipeServerStream 
        /// </summary>
        public InternalPipeServer(string pipeName)
        {
            _pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 254, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
            Writer = new StreamWriter(_pipeServer);
            Id = Guid.NewGuid().ToString();
        }

        #endregion

        #region events

        public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEvent;
        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        #endregion

        #region public methods

        public string ServerId => Id;

        /// <summary>
        /// This method begins an asynchronous operation to wait for a client to connect.
        /// </summary>
        public void Start()
        {
            _pipeServer.BeginWaitForConnection(WaitForConnectionCallBack, null);
        }

        /// <summary>
        /// This method disconnects, closes and disposes the server
        /// </summary>
        public void Stop()
        {
            _isStopping = true;

            try
            {
                if (_pipeServer.IsConnected)
                {
                    _pipeServer.Disconnect();
                }
            }
            finally
            {
                _pipeServer.Close();
                _pipeServer.Dispose();
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// This method begins an asynchronous read operation.
        /// </summary>
        private void BeginRead(Info info)
        {
            _pipeServer.BeginRead(info.Buffer, 0, BufferSize, EndReadCallBack, info);
        }

        /// <summary>
        /// This callback is called when the async WaitForConnection operation is completed,
        /// whether a connection was made or not. WaitForConnection can be completed when the server disconnects.
        /// </summary>
        private void WaitForConnectionCallBack(IAsyncResult result)
        {
            if (!_isStopping)
            {
                lock (_lockingObject)
                {
                    if (!_isStopping)
                    {
                        // Call EndWaitForConnection to complete the connection operation
                        _pipeServer.EndWaitForConnection(result);

                        OnConnected();

                        BeginRead(new Info());
                    }
                }
            }
        }

        /// <summary>
        /// This callback is called when the BeginRead operation is completed.
        /// We can arrive here whether the connection is valid or not
        /// </summary>
        private void EndReadCallBack(IAsyncResult result)
        {
            var readBytes = _pipeServer.EndRead(result);
            if (readBytes > 0)
            {
                var info = (Info) result.AsyncState;

                // Get the read bytes and append them
                info.StringBuilder.Append(Encoding.UTF8.GetString(info.Buffer, 0, readBytes));

                if (!_pipeServer.IsMessageComplete) // Message is not complete, continue reading
                {
                    BeginRead(info);
                }
                else // Message is completed
                {
                    // Finalize the received string and fire MessageReceivedEvent
                    var message = info.StringBuilder.ToString().TrimEnd('\0');

                    OnMessageReceived(message);

                    // Begin a new reading operation
                    BeginRead(new Info());
                }
            }
            else // When no bytes were read, it can mean that the client have been disconnected
            {
                if (!_isStopping)
                {
                    lock (_lockingObject)
                    {
                        if (!_isStopping)
                        {
                            OnDisconnected();
                            Stop();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method fires MessageReceivedEvent with the given message
        /// </summary>
        private void OnMessageReceived(string message)
        {
            MessageReceivedEvent?.Invoke(this, new MessageReceivedEventArgs {Message = message, Writer = Writer, Flush = Flush});
        }

        /// <summary>
        /// This method fires ConnectedEvent 
        /// </summary>
        private void OnConnected()
        {
            ClientConnectedEvent?.Invoke(this, new ClientConnectedEventArgs {ClientId = Id});
        }

        /// <summary>
        /// This method fires DisconnectedEvent 
        /// </summary>
        private void OnDisconnected()
        {
            ClientDisconnectedEvent?.Invoke(this, new ClientDisconnectedEventArgs {ClientId = Id});
        }

        #endregion

        public void Flush()
        {
            Writer.Flush();
            _pipeServer.Flush();
            Stop();
        }

        public void Dispose()
        {
            try
            {
                Writer.Dispose();
                _pipeServer.Dispose();
            }
            catch
            {
                // ignore, failures we dont care about (just trying to clean up nicely)
            }
        }
    }
}
