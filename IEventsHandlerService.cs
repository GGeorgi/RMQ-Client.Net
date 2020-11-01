using System.Threading.Tasks;

namespace RMQClient.Net
{
    public interface IEventsHandlerService
    {
        Task<dynamic> Handle(Message messageData);
    }
}