// File: Connection.cs
// Created: 27.04.2017
// 
// See <summary> tags for more information.

using System;
using System.Net.Sockets;

namespace NETworks
{
    internal class Connection
    {
        public ConnectionState State { get; set; }

        public string Guid { get; private set; }

        public string Service { get; private set; }

        internal TcpClient TcpClient { get; private set; }
        internal NetworkStream Stream { get; private set; }

        internal Connection(string guid, string service, TcpClient tcpClient, NetworkStream stream)
        {
            this.State = ConnectionState.Open;
            this.Guid = guid;
            this.Service = service;
            this.TcpClient = tcpClient;
            this.Stream = stream;
        }

        public void Close()
        {
            this.Stream.Dispose();
            this.TcpClient.Close();
            this.State = ConnectionState.Closed;
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