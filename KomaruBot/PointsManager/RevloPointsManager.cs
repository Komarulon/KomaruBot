using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KomaruBot.PointsManager
{
    public class RevloPointsManager : IPointsManager
    {
        private string apiKey;
        private string currencyPlural;
        private string currencySingular;
        public RevloPointsManager(
            string apiKey,
            string currencyPlural,
            string currencySingular
            )
        {
            this.apiKey = apiKey;
            this.currencyPlural = currencyPlural;
            this.currencySingular = currencySingular;
        }

        public long GetCurrentPlayerPoints(string userName)
        {
            throw new NotImplementedException();
        }

        public void GivePlayerPoints(string userName, long amount)
        {
            try
            {
                HttpClient client = new HttpClient();

                var request = new HttpRequestMessage(new HttpMethod("POST"), $"https://api.revlo.co/1/fans/{userName}/points/bonus");
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Host = "api.revlo.co";
                request.Headers.ConnectionClose = true;
                request.Headers.Add("x-api-key", apiKey);
                request.Content = new StringContent($"{{\"amount\": {amount}}}");

                var content = client.SendAsync(request).Result;
            }
            catch (Exception)
            {
                Logging.LogMessage($"Unable to award {userName} {amount} {(amount == 1 ? currencySingular : currencyPlural)}. Please do so manually.", true);

            }
        }
    }
}
