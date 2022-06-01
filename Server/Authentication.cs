using Newtonsoft.Json.Linq;
using RestSharp;

namespace Server
{
    public class Authentication
    {
        public string GetToken(string clientId, string clientSecret)
        {
            var client = new RestClient("https://api.signicat.io/oauth/connect/token");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            request.AddParameter("application/x-www-form-urlencoded", "grant_type=client_credentials&client_id="+ clientId +"&client_secret=" + clientSecret, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            dynamic resp = JObject.Parse(response.Content);
            string token = resp.access_token;
            return token;
        }
    }
}