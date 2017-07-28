using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KomaruBot.PointsManager
{
    public class StreamElementsPointsManager : IPointsManager
    {
        private string apiKey;
        private string currencyPlural;
        private string currencySingular;
        private string channelName;
        public StreamElementsPointsManager(
            string apiKey,
            string currencyPlural,
            string currencySingular,
            string channelName
            )
        {
            this.apiKey = apiKey;
            this.currencyPlural = currencyPlural;
            this.currencySingular = currencySingular;
            this.channelName = channelName;
        }


        public void GivePlayerPoints(string userName, long amount)
        {
            try
            {
                HttpClient client = new HttpClient();

                var request = new HttpRequestMessage(new HttpMethod("PUT"), $"https://api.streamelements.com/kappa/v1/points/{userName}/{amount}");
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var content = client.SendAsync(request).Result;
                //string responseBody = content.Content.ReadAsStringAsync().Result;
            }
            catch (Exception)
            {
                Logging.LogMessage($"Unable to award {userName} {amount} {(amount == 1 ? currencySingular : currencyPlural)}. Please do so manually.", true);
            }
        }

        public long GetCurrentPlayerPoints(string userName)
        {
            try
            {
                HttpClient client = new HttpClient();

                var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://api.streamelements.com/kappa/v1/points/{channelName}/{userName}");
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var content = client.SendAsync(request).Result;
                string responseBody = content.Content.ReadAsStringAsync().Result;

                var points = Newtonsoft.Json.JsonConvert.DeserializeObject<pointsContainer>(responseBody);
                return points.points;
            }
            catch (Exception)
            {
                Logging.LogMessage($"Unable to get points for {userName}. Please do so manually.", true);
                return 0;
            }
        }

        private class pointsContainer
        {
            public string channel { get; set; }
            public string username { get; set; }
            public long points { get; set; }
            public long pointsAlltime { get; set; }
            public long rank { get; set; }
        }
    }
}

