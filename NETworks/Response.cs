// File: Response.cs
// Created: 26.04.2017
// 
// See <summary> tags for more information.

namespace NETworks
{
    public class Response
    {
        public Response(ResponseStatus status, byte[] body, string message)
        {
            this.Status = status;
            this.Body = body;
            this.Message = message;
        }

        public ResponseStatus Status { get; private set; }

        public byte[] Body { get; private set; }

        public string Message { get; private set; }
    }

    public enum ResponseStatus
    {
        Ok,
        ServerError,
        Error
    }
}