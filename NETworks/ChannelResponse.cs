// File: ChannelResponse.cs
// Created: 27.04.2017
// 
// See <summary> tags for more information.

using System.IO;

namespace NETworks
{
    public class ChannelResponse
    {
        public ChannelResponse(Stream stream, ChannelStatus status)
        {
            this.Stream = stream;
            this.Status = status;
        }

        public Stream Stream { get; private set; }

        public ChannelStatus Status { get; private set; }
    }

    public enum ChannelStatus
    {
        Open,
        Denied,
        Error
    }
}