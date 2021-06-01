using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;

namespace RMQClient.Net.Handlers
{
    public class RequestHandler : EventHandler
    {
        private ConcurrentDictionary<IConvertible, Func<Message, Task<dynamic?>>> RequestMapper { get; set; }

        public RequestHandler()
        {
            RequestMapper = new ConcurrentDictionary<IConvertible, Func<Message, Task<dynamic?>>>();
        }

        protected void HandleRequest(IConvertible code, Func<Message, Task<dynamic?>> action)
        {
            Codes.Add(code);
            RequestMapper.TryAdd(code.ToInt32(new NumberFormatInfo()), action);
        }

        internal async Task<dynamic?> Handle(IConvertible code, dynamic data)
        {
            if (RequestMapper.TryGetValue(code, out var action))
            {
                return await action.Invoke(data);
            }

            return null;
        }
    }
}