using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RMQClient.Net.Handlers;
using Microsoft.Extensions.Hosting;
using EventHandler = RMQClient.Net.Handlers.EventHandler;

namespace RMQClient.Net
{
    public sealed class RPCServer : IHostedService
    {
        private readonly IEnumerable<EventHandler> _handlers;
        private readonly RequestHandler? _requestHandler;
        private const string EXCHANGE = "updates";
        private readonly string _queue;
        private readonly string _queueExceptions;
        private readonly IModel _channel;
        private readonly IConnection _connection;
        private readonly ushort _count;
        private readonly List<IConvertible> _events;

        public RPCServer(
            IConfiguration config,
            IEnumerable<EventHandler> handlers,
            RequestHandler requestHandler = null!)
        {
            _handlers = handlers;
            _requestHandler = requestHandler;
            _queue = config.GetValue<string>("RabbitMQ:Queue");
            _queueExceptions = $"{_queue}Exceptions";

            ConnectionFactory factory = new()
            {
                HostName = config.GetValue<string>("RabbitMQ:Host"),
                UserName = config.GetValue<string>("RabbitMQ:Username"),
                Password = config.GetValue<string>("RabbitMQ:Password"),
                Port = config.GetValue<int>("RabbitMQ:Port"),
                VirtualHost = config.GetValue<string>("RabbitMQ:Vhost"),
                AutomaticRecoveryEnabled = true,
            };
            _count = config.GetValue<ushort?>("RabbitMQ:Prefetch") ?? 100;

            _events = _handlers.SelectMany(x => x.Codes).ToList();
            if (_requestHandler != null)
            {
                _events.AddRange(_requestHandler.Codes);
            }

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }


        private void Subscribe(IEnumerable<IConvertible> events)
        {
            _channel.ExchangeDeclare(EXCHANGE, ExchangeType.Direct);
            _channel.QueueDeclare(_queue, false, false, false, null);
            _channel.QueueDeclare(_queueExceptions, false, false, false, null);
            foreach (var item in events)
            {
                _channel.QueueBind(_queue, EXCHANGE, item.ToString(CultureInfo.InvariantCulture));
            }

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                var deserializedMessage = JsonConvert.DeserializeObject<Message>(message);
                var props = ea.BasicProperties;
                var replyProps = _channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                #region notify subscribers

                foreach (var eventHandler in _handlers)
                {
                    eventHandler.Notify(deserializedMessage.Code, deserializedMessage);
                }

                #endregion

                #region Send Response

                if (_requestHandler != null)
                {
                    try
                    {
                        var response = await _requestHandler.Handle(deserializedMessage.Code, deserializedMessage);
                        if (response != null && props.CorrelationId != null && props.ReplyTo != null)
                        {
                            var messageData = new Message
                            {
                                Body = response!,
                                Code = deserializedMessage.Code
                            };
                            var replyMessage = JsonConvert.SerializeObject(messageData);
                            var replyMessageBytes = Encoding.UTF8.GetBytes(replyMessage);

                            _channel.BasicPublish("", props.ReplyTo, replyProps, replyMessageBytes);
                        }
                    }
                    catch (Exception e)
                    {
                        if (props.CorrelationId != null && props.ReplyTo != null)
                        {
                            var errorReply = JsonConvert.SerializeObject(new Message
                            {
                                Body = e.Message,
                                Code = deserializedMessage.Code,
                                Error = true
                            });
                            var errorReplyBytes = Encoding.UTF8.GetBytes(errorReply);

                            _channel.BasicPublish("", props.ReplyTo, replyProps, errorReplyBytes);
                        }
                        else
                        {
                            var replyMessage = JsonConvert.SerializeObject(new Message
                            {
                                Body = e,
                                Code = deserializedMessage.Code,
                                Error = true
                            });
                            var replyMessageBytes = Encoding.UTF8.GetBytes(replyMessage);
                            _channel.BasicPublish("", _queueExceptions, replyProps, replyMessageBytes);
                        }
                    }
                }

                #endregion

                _channel.BasicAck(ea.DeliveryTag, false);
            };
            _channel.BasicQos(0, _count, true);
            _channel.BasicConsume(_queue, false, consumer);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Subscribe(_events);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // close
            _channel.Close();
            _connection.Close();
            // dispose
            _channel.Dispose();
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}