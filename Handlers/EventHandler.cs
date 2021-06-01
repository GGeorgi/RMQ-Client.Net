using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace RMQClient.Net.Handlers
{
    public class EventHandler
    {
        internal List<IConvertible> Codes { get; }
        private ConcurrentDictionary<IConvertible, Action<Message>> CallbackMapper { get; set; }

        protected EventHandler()
        {
            CallbackMapper = new ConcurrentDictionary<IConvertible, Action<Message>>();
            Codes = new List<IConvertible>();
        }

        public void HandleEvent(IConvertible code, Action<Message> action)
        {
            Codes.Add(code);
            CallbackMapper.TryAdd(code.ToInt32(new NumberFormatInfo()), action);
        }

        internal void Notify(IConvertible code, Message data)
        {
            if (CallbackMapper.TryGetValue(code, out var action))
            {
                Task.Run(() => action.Invoke(data));
            }
        }
    }
}