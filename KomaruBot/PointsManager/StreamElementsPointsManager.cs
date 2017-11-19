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
        private string streamElementsAccountID;
        public StreamElementsPointsManager(
            string apiKey,
            string currencyPlural,
            string currencySingular,
            string streamElementsAccountID
            )
        {
            this.apiKey = apiKey;
            this.currencyPlural = currencyPlural;
            this.currencySingular = currencySingular;
            this.streamElementsAccountID = streamElementsAccountID;
        }


        public void GivePlayerPoints(string userName, long amount, out long? newAmount)
        {
            try
            {
                HttpClient client = new HttpClient();

                var request = new HttpRequestMessage(new HttpMethod("PUT"), $"https://api.streamelements.com/kappa/v2/points/{streamElementsAccountID}/{userName}/{amount}");
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var content = client.SendAsync(request).Result;
                string responseBody = content.Content.ReadAsStringAsync().Result;

                if (content.IsSuccessStatusCode)
                {
                    var points = Newtonsoft.Json.JsonConvert.DeserializeObject<newPointsContainer>(responseBody);
                    newAmount = points.newAmount;
                }
                else
                {
                    throw new Exception($"Response status code ({content.StatusCode}) was not a success code. Response body: {responseBody}");
                }
            }
            catch (Exception exc)
            {
                Logging.LogMessage($"Unable to award {userName} {amount} {(amount == 1 ? currencySingular : currencyPlural)}. Please do so manually.", true);
                Logging.LogException(exc, "Exception Details: ");
                newAmount = null;
            }
        }

        public long GetCurrentPlayerPoints(string userName)
        {
            try
            {
                HttpClient client = new HttpClient();

                var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://api.streamelements.com/kappa/v2/points/{streamElementsAccountID}/{userName}");
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var content = client.SendAsync(request).Result;
                string responseBody = content.Content.ReadAsStringAsync().Result;

                if (content.IsSuccessStatusCode)
                {
                    var points = Newtonsoft.Json.JsonConvert.DeserializeObject<pointsContainer>(responseBody);
                    return points.points;
                }
                // TODO: figure out if this is dumb or not
                // I'm not sure if a user who just shows up will be found or not
                // and if they're showing up as NotFound than this is good
                // but this stops all logging for legit 404 responses
                else if (content.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return 0;
                }
                else
                {
                    throw new Exception($"Response status code ({content.StatusCode}) was not a success code. Response body: {responseBody}");
                }
            }
            catch (Exception exc)
            {
                Logging.LogMessage($"Unable to get points for {userName}. Please do so manually.", true);
                Logging.LogException(exc, "Exception Details: ");
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

        private class newPointsContainer
        {
            public string channel { get; set; }
            public string username { get; set; }
            public long amount { get; set; }
            public long newAmount { get; set; }
            public string message { get; set; }
        }
    }
}

