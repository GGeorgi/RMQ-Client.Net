1. RPClient required only for sending request and getting request's response.
2. RPCServer required only for accepting requests.

## Usage

#### Startup.cs

```c#
public void ConfigureServices(IServiceCollection services)
{
    var events = new[]
    {
        1, 2, 3
    };
    services
        .AddSingleton<EventsHandlerService>()
        .AddHostedService(x =>
            new RPCServer(Configuration, x.GetRequiredService<EventsHandlerService>(), events))
        .AddSingleton<RPCClient>();
}
```

#### EventsHandlerService.cs Example

```c#
public class EventsHandlerService : IEventsHandlerService
{
    private readonly IServiceProvider _serviceProvider;

    public EventsHandlerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<dynamic> Handle(Message e)
    {
        var code = e.Code;
        switch (code)
        {
            case 1:
                // do some work 1
                break;
            case 2:
                // do some work 2 and return value
                return new {Done = true};
        }

        return null;
    }
}
```

#### Request/Response Example

```c#
class Response
{
    public bool Done { get; set; }
}

...

class SomeClass
{
    public Constructor(RPCClient client)
    {
        _client = client;
    }
    
    ...

    public async Task SomeRequest()
    {
        await _client.CallAsync("service_queue_name", 1, new {Id = 1, Name = "Test"});
        
        var result = await _client.CallAsync<Response>("service_queue_name", 2, new {Id = 1, Name = "Test"});
        
        Console.WriteLine(result.Done)
    }
}
```