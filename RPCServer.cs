using System;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RMQClient.Net
{
    public sealed class RPCServer : IHostedService
    {
        private const string exchange = "rmqclient-updates";
        private readonly string queue;
        private readonly string queueExceptions;
        private IModel _channel;
        private readonly IEventsHandlerService handler;
        private readonly int[] _events;
        private readonly ConnectionFactory _factory;
        private IConnection _connection;

        public RPCServer(IConfiguration config, IEventsHandlerService handler, int[] events = null)
        {
            this.handler = handler;
            queue = config.GetValue<string>("RabbitMQ:Queue");
            queueExceptions = $"{queue}Exceptions";
            _events = events;
            _factory = new ConnectionFactory
            {
                HostName = config.GetValue<string>("RabbitMQ:Host"),
                UserName = config.GetValue<string>("RabbitMQ:Username"),
                Password = config.GetValue<string>("RabbitMQ:Password"),
                Port = config.GetValue<int>("RabbitMQ:Port"),
                VirtualHost = config.GetValue<string>("RabbitMQ:Vhost"),
                AutomaticRecoveryEnabled = true
            };
        }


        private void Subscribe(int[] events)
        {
            _channel.ExchangeDeclare(exchange, ExchangeType.Direct);
            _channel.QueueDeclare(queue, false, false, false, null);
            _channel.QueueDeclare(queueExceptions, false, false, false, null);
            if (events != null)
            {
                foreach (var item in events)
                {
                    _channel.QueueBind(queue, exchange, item.ToString());
                }
            }

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                var deserializedMessage = JsonConvert.DeserializeObject<Message>(message);
                var props = ea.BasicProperties;
                var replyProps = _channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;
                try
                {
                    var response = await handler.Handle(deserializedMessage);
                    if (props.CorrelationId != null && props.ReplyTo != null)
                    {
                        var messageData = new Message
                        {
                            Body = response,
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
                        _channel.BasicPublish("", queueExceptions, replyProps, replyMessageBytes);
                    }
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            };
            _channel.BasicQos(0, 100, true);
            _channel.BasicConsume(queue, false, consumer);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
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