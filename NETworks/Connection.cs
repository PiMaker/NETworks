// File: Connection.cs
// Created: 27.04.2017
// 
// See <summary> tags for more information.

using System.Net.Sockets;
using SimpleLogger;

namespace NETworks
{
    internal class Connection
    {
        internal Connection(string guid, string service, TcpClient tcpClient, NetworkStream stream)
        {
            this.State = ConnectionState.Open;
            this.Guid = guid;
            this.Service = service;
            this.TcpClient = tcpClient;
            this.Stream = stream;
        }

        public ConnectionState State { get; set; }

        public string Guid { get; }

        public string Service { get; private set; }

        internal TcpClient TcpClient { get; }

        internal NetworkStream Stream { get; }

        public void Close()
        {
            Logger.Log($"Connection {this.Guid} closing");
            this.State = ConnectionState.Closed;
            this.Stream.Close();
            this.Stream.Dispose();
            this.TcpClient.Close();
        }

        public override string ToString()
        {
            return $"Connection ({this.Guid}) <> {this.State}";
        }
    }

    public enum ConnectionState
    {
        Open,
        Closed
    }
}