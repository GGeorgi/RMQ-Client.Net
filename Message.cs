using System;

namespace RMQClient.Net
{
    public class Message<T> : EventArgs
    {
        public T Body { get; set; }
        public int Code { get; set; }
        public bool Error { get; set; }
    }

    public class Message : EventArgs
    {
        public dynamic Body { get; set; }
        public int Code { get; set; }
        public bool Error { get; set; }
    }
}