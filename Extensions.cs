using System;
using RMQClient.Net.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using EventHandler = RMQClient.Net.Handlers.EventHandler;

namespace RMQClient.Net
{
    public static class Extensions
    {
        public static T[] DeserializeList<T>(this Message message)
        {
            return ((JArray) message.Body).ToObject<T[]>() ?? Array.Empty<T>();
        }

        public static T Deserialize<T>(this Message e)
        {
            return ((JObject) e.Body).ToObject<T>()!;
        }

        public static IServiceCollection AddEventHandler<TEventHandler>(this IServiceCollection serviceCollection)
            where TEventHandler : EventHandler
        {
            serviceCollection.AddSingleton<EventHandler, TEventHandler>();
            return serviceCollection;
        }

        public static IServiceCollection AddRequestHandler<TRequestHandler>(this IServiceCollection serviceCollection)
            where TRequestHandler : RequestHandler
        {
            serviceCollection.AddSingleton<RequestHandler, TRequestHandler>();
            return serviceCollection;
        }
    }
}