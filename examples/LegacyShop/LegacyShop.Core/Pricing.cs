using System.Configuration;
using System.Net;

namespace LegacyShop.Core
{
    public class Pricing
    {
        public decimal UsdRate()
        {
            var url = ConfigurationManager.AppSettings["RatesUrl"];
            using (var client = new WebClient())
            {
                return decimal.Parse(client.DownloadString(url));
            }
        }
    }
}
