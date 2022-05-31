using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json.Linq;

namespace Server
{
    class Server
    {
        public static void Main(string[] args)
        {
            WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://localhost:4242")
                .UseWebRoot(".")
                .UseStartup<Startup>()
                .Build()
                .Run();
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseStaticFiles();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }


    [Route("authentication-session")]
    [ApiController]
    public class AuthenticationApiController : Controller
    {
        private readonly string _frontendAppUrl;
        private readonly string _backendUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;

        public AuthenticationApiController(IConfiguration configuration)
        {
            _frontendAppUrl = configuration["FrontendAppUrl"];
            _backendUrl = configuration["BackendUrl"];
            _clientId = configuration["Signicat:ClientId"];
            _clientSecret = configuration["Signicat:ClientSecret"];
        }

        [HttpPost]
        public async Task<ActionResult> Create()
        {
            // Get Access Token
            string token = new Authentication().GetToken(_clientId, _clientSecret);

            // Create an authentication session
            var authClient = new RestClient("https://api.signicat.io/identification/v2/sessions");
            var authRequest = new RestRequest(Method.POST);
            authRequest.AddHeader("Authorization", "Bearer " + token);
            authRequest.AddHeader("Content-Type", "application/json");
            var body = @"{" + "\n" +
                @"  ""flow"": ""redirect""," + "\n" +
                @"  ""allowedProviders"": [" + "\n" +
                @"    ""no_bankid_netcentric""," + "\n" +
                @"    ""no_bankid_mobile""" + "\n" +
                @"  ]," + "\n" +
                @"  ""include"": [" + "\n" +
                @"    ""name""," + "\n" +
                @"    ""date_of_birth""," + "\n" +
                @"    ""nin""" + "\n" +
                @"  ]," + "\n" +
                @"  ""redirectSettings"": {" + "\n" +
                @"    ""successUrl"":  """ +  _backendUrl + @"authentication-session""," + "\n" +
                @"    ""abortUrl"": ""https://developer.signicat.io/landing-pages/something-wrong.html""," + "\n" +
                @"    ""errorUrl"": ""https://developer.signicat.io/landing-pages/something-wrong.html""" + "\n" +
                @"  }" + "\n" +
                @"}";
            
           
            authRequest.AddParameter("application/json", body, ParameterType.RequestBody);

            // Execute the request
            IRestResponse authResponse = await authClient.ExecuteAsync(authRequest);

            // Open the returned URL
            dynamic response = JObject.Parse(authResponse.Content);
            string url = response.url;

            Response.Headers.Add("Location", url);
            return new StatusCodeResult(303);
        }

        [HttpGet]
        public async Task<ActionResult> Retrieve([FromQuery(Name = "sessionId")] string sessionId)
        {
            // Get Access Token
            string token = new Authentication().GetToken(_clientId, _clientSecret);

            // Get nin (national identity number) using the session id
            var ninClient = new RestClient("https://api.signicat.io/identification/v2/sessions/" + sessionId);
            var ninRequest = new RestRequest(Method.GET);
            ninRequest.AddHeader("Authorization", "Bearer " + token);
            IRestResponse ninResponse = ninClient.Execute(ninRequest);
            
            JObject response = JObject.Parse(ninResponse.Content);
            var nin = response["identity"]["nin"];

            // Registry lookup with the nin
            var lookupClient = new RestClient("https://api.signicat.io/information/countries/NO/persons/?identityNumber=" + nin );
            var lookupRequest = new RestRequest(Method.GET);
            lookupRequest.AddHeader("Authorization", "Bearer " + token);
            IRestResponse lookupResponse = lookupClient.Execute(lookupRequest);
            
            // Check if lookup response is successful
            if (!lookupResponse.IsSuccessful)
            {
                Response.Headers.Add("Location", _frontendAppUrl + "?error=true");
                return new StatusCodeResult(303);
            }
            
            JObject personInfo = JObject.Parse(lookupResponse.Content);

            string personFirstName = (string)personInfo["names"][0]["first"];
            string personLastName = (string)personInfo["names"][0]["last"];
            string personMiddleName = string.IsNullOrEmpty((string)personInfo["names"][0]["middle"]) ? " " : (string)personInfo["names"][0]["middle"];
            string personBirth = string.IsNullOrEmpty((string)personInfo["birth"]["location"]) ? "n.a." : (string)personInfo["birth"]["location"];
            string personBirthDate = (string)personInfo["birth"]["dateOfBirth"];
            string personBirthCountry = personInfo["birth"]["country"].HasValues ? (string)personInfo["birth"]["country"]["alpha3"] : "n.a.";
            string personCitizenship = personInfo["citizenships"].HasValues ? (string)personInfo["citizenships"][0]["country"]["alpha3"] : "n.a.";
            string personStreet = personInfo["permanentAddresses"].HasValues ? (string)personInfo["permanentAddresses"][0]["street"] : "n.a.";
            string personPostalCode = personInfo["permanentAddresses"].HasValues ? (string)personInfo["permanentAddresses"][0]["postalCode"] : " ";
            string personCity = personInfo["permanentAddresses"].HasValues ? (string)personInfo["permanentAddresses"][0]["city"] : " ";
            string personCountry = personInfo["permanentAddresses"].HasValues ? (string)personInfo["permanentAddresses"][0]["country"]["alpha3"] : " ";
            string lookupSource = (string)personInfo["metadata"]["sources"][0];
            
            var encodedFirstName = Uri.EscapeDataString(personFirstName);
            var encodedLastName = Uri.EscapeDataString(personLastName);
            var encodedMiddleName = Uri.EscapeDataString(personMiddleName);
            var encodedBirth = Uri.EscapeDataString(personBirth);
            var encodedPersonStreet = Uri.EscapeDataString(personStreet);
            var encodedPersonCity = Uri.EscapeDataString(personCity);

            Response.Headers.Add("Location", _frontendAppUrl + "?success=true&name=" + encodedFirstName + "&lastName=" + encodedLastName + "&middleName=" + encodedMiddleName + "&nin=" + nin + "&birth=" + encodedBirth +
                "&birthDate=" + personBirthDate + "&birthCountry=" + personBirthCountry + "&citizenship=" + personCitizenship + "&personStreet=" + encodedPersonStreet +
                "&personPostalCode=" + personPostalCode + "&personCity=" + encodedPersonCity + "&personCountry=" + personCountry + "&lookupSource=" + lookupSource);
            return new StatusCodeResult(303);
        }
    }
}