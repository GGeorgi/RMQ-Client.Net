using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace RMQClient.Net
{
    public class RPCClient
    {
        private const string exchange = "rmqclient-updates";
        private readonly IModel channel;
        private readonly string replyQueueName;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper =
            new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        public RPCClient(IConfiguration config)
        {
            var factory = new ConnectionFactory
            {
                HostName = config.GetValue<string>("RabbitMQ:Host"),
                UserName = config.GetValue<string>("RabbitMQ:Username"),
                Password = config.GetValue<string>("RabbitMQ:Password"),
                Port = config.GetValue<int>("RabbitMQ:Port"),
                VirtualHost = config.GetValue<string>("RabbitMQ:Vhost"),
                AutomaticRecoveryEnabled = true
            };

            var connection = factory.CreateConnection();
            channel = connection.CreateModel();
            replyQueueName = channel.QueueDeclare().QueueName;
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out TaskCompletionSource<string> tcs))
                    return;
                var body = ea.Body;
                var response = Encoding.UTF8.GetString(body);

                var deserializedMessage = JsonConvert.DeserializeObject<Message>(response);
                if (deserializedMessage.Error)
                    tcs.SetException(new Exception(deserializedMessage.Body as string));
                else tcs.TrySetResult(response);
            };

            channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);
        }

        public Task<string> CallAsync(string queue, int code, dynamic body,
            CancellationToken cancellationToken = default)
        {
            var props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;
            var messageData = new Message
            {
                Body = body,
                Code = code
            };
            var message = JsonConvert.SerializeObject(messageData);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            var tcs = new TaskCompletionSource<string>();
            callbackMapper.TryAdd(correlationId, tcs);

            channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: props,
                body: messageBytes);

            cancellationToken.Register(() => callbackMapper.TryRemove(correlationId, out _));
            return tcs.Task;
        }

        public async Task<T> CallAsync<T>(string queue, int code, dynamic body,
            CancellationToken cancellationToken = default)
        {
            var str = await CallAsync(queue, code, body, cancellationToken);
            var deserializedMessage = JsonConvert.DeserializeObject<Message<T>>(str);
            return deserializedMessage.Body;
        }
        
        public void PublishEvent<T>(int code, T body)
        {
            var messageData = new Message<T>()
            {
                Body = body,
                Code = code
            };
            var message = JsonConvert.SerializeObject(messageData);
            var messageBody = Encoding.UTF8.GetBytes(message);

            var props = channel.CreateBasicProperties();

            channel.BasicPublish(exchange: exchange,
                routingKey: code.ToString(),
                basicProperties: props,
                body: messageBody);
        }
    }
}