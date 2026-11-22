using System.Runtime.Remoting;
using System.ServiceModel;

namespace LegacyShop.Services
{
    [ServiceContract]
    public interface IOrderService
    {
        [OperationContract]
        string Status(int orderId);
    }

    public class OrderService : IOrderService
    {
        public string Status(int orderId) => "shipped";
    }

    public static class LegacyBridge
    {
        public static void Configure() => RemotingConfiguration.Configure("bridge.config", false);
    }
}
