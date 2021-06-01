using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RMQClient.Net
{
    public class RPCClient
    {
        private const string EXCHANGE = "updates";
        private readonly IModel _channel;
        private readonly string _replyQueueName;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper = new();

        public RPCClient(IConfiguration config)
        {
            var factory = new ConnectionFactory()
            {
                HostName = config.GetValue<string>("RabbitMQ:Host"),
                UserName = config.GetValue<string>("RabbitMQ:Username"),
                Password = config.GetValue<string>("RabbitMQ:Password"),
                Port = config.GetValue<int>("RabbitMQ:Port"),
                VirtualHost = config.GetValue<string>("RabbitMQ:Vhost"),
                AutomaticRecoveryEnabled = true
            };

            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();
            _replyQueueName = _channel.QueueDeclare().QueueName;
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, ea) =>
            {
                if (!_callbackMapper.TryRemove(ea.BasicProperties.CorrelationId,
                    out TaskCompletionSource<string>? tcs)) return;
                var body = ea.Body;
                var response = Encoding.UTF8.GetString(body);

                var deserializedMessage = JsonConvert.DeserializeObject<Message>(response);
                if (deserializedMessage.Error)
                    tcs.SetException(new Exception(deserializedMessage.Body as string));
                else tcs.TrySetResult(response);
            };

            _channel.BasicConsume(
                consumer: consumer,
                queue: _replyQueueName,
                autoAck: true);
        }

        public Task<string> CallAsync(IConvertible queue, IConvertible code, dynamic body,
            CancellationToken cancellationToken = default)
        {
            var props = _channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = _replyQueueName;
            var messageData = new Message
            {
                Body = body,
                Code = code
            };
            var message = JsonConvert.SerializeObject(messageData);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            var tcs = new TaskCompletionSource<string>();
            _callbackMapper.TryAdd(correlationId, tcs);

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue.ToString(CultureInfo.InvariantCulture),
                basicProperties: props,
                body: messageBytes);

            cancellationToken.Register(() => _callbackMapper.TryRemove(correlationId, out _));
            return tcs.Task;
        }

        public async Task<T> CallAsync<T>(IConvertible queue, IConvertible code, dynamic body,
            CancellationToken cancellationToken = default)
        {
            var str = await CallAsync(queue, code, body, cancellationToken);
            var deserializedMessage = JsonConvert.DeserializeObject<Message<T>>(str);
            return deserializedMessage.Body;
        }

        public void PublishEvent<T>(IConvertible code, T body)
        {
            var messageData = new Message<T>()
            {
                Body = body,
                Code = code
            };
            var message = JsonConvert.SerializeObject(messageData);
            var messageBody = Encoding.UTF8.GetBytes(message);

            var props = _channel.CreateBasicProperties();

            _channel.BasicPublish(exchange: EXCHANGE,
                routingKey: code.ToString(CultureInfo.InvariantCulture),
                basicProperties: props,
                body: messageBody);
        }

        public void PublishEvent<T>(string exchange, T body)
        {
            var message = JsonConvert.SerializeObject(body);
            var messageBody = Encoding.UTF8.GetBytes(message);

            var props = _channel.CreateBasicProperties();

            _channel.BasicPublish(exchange: exchange,
                routingKey: "",
                basicProperties: props,
                body: messageBody);
        }
    }
}