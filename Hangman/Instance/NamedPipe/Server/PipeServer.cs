﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Hangman.Instance.NamedPipe.Interfaces;

namespace Hangman.Instance.NamedPipe.Server
{
    public class PipeServer : ICommunicationServer
    {
        #region private fields

        private readonly string _pipeName;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly IDictionary<string, ICommunicationServer> _servers;

        #endregion

        #region c'tor

        public PipeServer(string pipeName)
        {
            _pipeName = pipeName;
            _synchronizationContext = AsyncOperationManager.SynchronizationContext;
            _servers = new ConcurrentDictionary<string, ICommunicationServer>();
        }

        #endregion

        #region events

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;
        public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEvent;

        #endregion

        #region ICommunicationServer implementation

        public string ServerId => _pipeName;

        public void Start()
        {
            StartNamedPipeServer();
        }

        public void Stop()
        {
            try
            {
                Exception e = null;
                foreach (var server in _servers.Values)
                {
                    try
                    {
                        UnregisterFromServerEvents(server);
                        server.Stop();
                    }
                    catch (Exception e2)
                    {
                        e = e2;
                    }
                }
                if (e != null)
                {
                    throw new Exception("Failed to stop server", e);
                }
            }
            finally
            {
                _servers.Clear();
            }
        }
        
        #endregion

        #region private methods

        /// <summary>
        /// Starts a new NamedPipeServerStream that waits for connection
        /// </summary>
        private void StartNamedPipeServer()
        {
            var server = new InternalPipeServer(_pipeName);
            _servers[server.Id] = server;

            server.ClientConnectedEvent += ClientConnectedHandler;
            server.ClientDisconnectedEvent += ClientDisconnectedHandler;
            server.MessageReceivedEvent += OnMessageReceived;

            server.Start();
        }

        /// <summary>
        /// Stops the server that belongs to the given id
        /// </summary>
        /// <param name="id"></param>
        private void StopNamedPipeServer(string id)
        {
            UnregisterFromServerEvents(_servers[id]);
            _servers[id].Stop();
            _servers.Remove(id);
        }

        /// <summary>
        /// Unregisters from the given server's events
        /// </summary>
        /// <param name="server"></param>
        private void UnregisterFromServerEvents(ICommunicationServer server)
        {
            server.ClientConnectedEvent -= ClientConnectedHandler;
            server.ClientDisconnectedEvent -= ClientDisconnectedHandler;
            server.MessageReceivedEvent -= OnMessageReceived;
        }

        /// <summary>
        /// Fires MessageReceivedEvent in the current thread
        /// </summary>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs messageReceivedEventArgs)
        {
            _synchronizationContext.Post(e => MessageReceivedEvent?.Invoke(this, (MessageReceivedEventArgs) e), messageReceivedEventArgs);
        }

        /// <summary>
        /// Fires ClientConnectedEvent in the current thread
        /// </summary>
        /// <param name="eventArgs"></param>
        private void OnClientConnected(ClientConnectedEventArgs eventArgs)
        {
            _synchronizationContext.Post(e => ClientConnectedEvent?.Invoke(this, (ClientConnectedEventArgs) e), eventArgs);
        }

        /// <summary>
        /// Fires ClientDisconnectedEvent in the current thread
        /// </summary>
        /// <param name="eventArgs"></param>
        private void OnClientDisconnected(ClientDisconnectedEventArgs eventArgs)
        {
            _synchronizationContext.Post(e => ClientDisconnectedEvent?.Invoke(this, (ClientDisconnectedEventArgs) e), eventArgs);
        }

        /// <summary>
        /// Handles a client connection. Fires the relevant event and prepares for new connection.
        /// </summary>
        private void ClientConnectedHandler(object sender, ClientConnectedEventArgs eventArgs)
        {
            OnClientConnected(eventArgs);
            StartNamedPipeServer(); // Create a additional server as a preparation for new connection
        }

        /// <summary>
        /// Hanldes a client disconnection. Fires the relevant event ans removes its server from the pool
        /// </summary>
        private void ClientDisconnectedHandler(object sender, ClientDisconnectedEventArgs eventArgs)
        {
            OnClientDisconnected(eventArgs);
            StopNamedPipeServer(eventArgs.ClientId);
        }

        #endregion
    }
}
