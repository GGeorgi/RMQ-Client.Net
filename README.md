## How To Use

###Event Driven Pattern
1. Add Config in ``appsettings.json``
```config
{
    "RabbitMQ": {
        "Host": "localhost",
        "Username": "username",
        "Password": "password",
        "Vhost": "for app vhost",
        "Port": 5672
    }
}
```
2. Register Broker in ``Startup.cs`` for listening exchange for events

````c#
public void ConfigureServices(IServiceCollection services){

...
services.AddHostedService<RPCServer>();
...

}
````

3. Create class derived from ``EventHandler`` for handling events. \
   **You can create as many EventHandlers as you want.** \
   All EventHandlers registered to particular event will get copy of incoming message.
```c#
public class EventHandlerService : EventHandler
{
    public EventHandlerService()
    {
        HandleEvent(CommunicationCodes.ANY_CODE, HandleExample);
    }

    private void HandleExample(Message msg)
    {
        // some logic related to incoming message
    }
}
```

4. Register ``EventHandlerService`` in ``Startup.cs``

````c#
public void ConfigureServices(IServiceCollection services){

...
services.AddRequestHandler<RequestHandlerService1>();
services.AddRequestHandler<RequestHandlerService2>();
services.AddRequestHandler<RequestHandlerService3>();
services.AddHostedService<RPCServer>();
...

}
````

###Request Response Pattern
1. Add Config in ``appsettings.json``
```config
{
    "RabbitMQ": {
        "Host": "localhost",
        "Username": "username",
        "Password": "password",
        "Vhost": "for app vhost",
        "Queue": "Queue to listen for incoming messages",
        "Port": 5672
    }
}
```

2. Register Broker in ``Startup.cs`` for listening incoming messages

````c#
public void ConfigureServices(IServiceCollection services){

...
services.AddHostedService<RPCServer>();
...

}
````

3. Create class derived from ``RequestHandler`` for handling requests. \
   **You need create ONLY ONE RequestHandler.** 
```c#
public class RequestHandlerService : RequestHandler
{
    public RequestHandlerService()
    {
        HandleRequest(CommunicationCodes.ANY_CODE, async msg => await HandleExample(msg));
    }

    private async Task<int> HandleExample(Message msg)
    {
        // some logic related to incoming message
        return 0;
    }
}
```

4. Register ``RequestHandlerService`` in ``Startup.cs``

````c#
public void ConfigureServices(IServiceCollection services){

...
services.AddRequestHandler<RequestHandlerService>();
services.AddHostedService<RPCServer>();
...

}
````


###Sending Requests and Events

1. Register ``RPCClient`` in ``Startup.cs``

````c#
public void ConfigureServices(IServiceCollection services){

...
services.AddSingleton<RPCClient>();
...

}
````

2. Send Request ``_broker.CallAsync<T>``
3. Publish Event ``_broker.PublishEvent`` 