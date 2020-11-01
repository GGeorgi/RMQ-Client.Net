1. RPClient need only to send request and get response for that request.
2. RPCServer needs for accepting requests.

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