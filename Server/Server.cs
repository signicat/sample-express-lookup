using System;
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Signicat.Express;
using Signicat.Express.IdentificationV2;
using RestSharp;
using Newtonsoft.Json;
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

            services.AddSingleton<IIdentificationV2Service>(c => new IdentificationV2Service(
                Configuration["Signicat:ClientId"],
                Configuration["Signicat:ClientSecret"],
                new List<OAuthScope>() { OAuthScope.Identify }
            ));
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
        private readonly IIdentificationV2Service _identificationService;
        private readonly string _frontendAppUrl;
        private readonly string _backendUrl;

        public AuthenticationApiController(IIdentificationV2Service identificationService, IConfiguration configuration)
        {
            _identificationService = identificationService;
            _frontendAppUrl = configuration["FrontendAppUrl"];
            _backendUrl = configuration["BackendUrl"];
        }

        [HttpPost]
        public async Task<ActionResult> Create()
        {
            // Get token
            var client = new RestClient("https://api.signicat.io/oauth/connect/token");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            // Client id and secret
            request.AddParameter("application/x-www-form-urlencoded", "grant_type=client_credentials&client_id=t184d407d352b4a1b9df643acff34cfab&client_secret=0KFHH9PZGitIIELlFYIcquCkGWTTRBVw", ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);

            // Extract access token from response
            dynamic resp = JObject.Parse(response.Content);
            string token = resp.access_token;

            //Make authentication call:
            var authclient = new RestClient("https://api.signicat.io/identification/v2/sessions");
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
                @"    ""successUrl"": ""http://localhost:4242/authentication-session""," + "\n" +
                @"    ""abortUrl"": ""https://developer.signicat.io/landing-pages/something-wrong.html""," + "\n" +
                @"    ""errorUrl"": ""https://developer.signicat.io/landing-pages/something-wrong.html""" + "\n" +
                @"  }" + "\n" +
                @"}";
            authRequest.AddParameter("application/json", body, ParameterType.RequestBody);

            // Execute the request
            IRestResponse authresponse = await authclient.ExecuteAsync(authRequest);

            // Open the returned URL
            dynamic authresp = JObject.Parse(authresponse.Content);
            string url = authresp.url;

            Response.Headers.Add("Location", url);
            return new StatusCodeResult(303);
        }

        [HttpGet]
        public async Task<ActionResult> Retrieve([FromQuery(Name = "sessionId")] string sessionId)
        {
            // get access token 
            var client = new RestClient("https://api.signicat.io/oauth/connect/token");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            
            // Client id and secret
            request.AddParameter("application/x-www-form-urlencoded", "grant_type=client_credentials&client_id=t184d407d352b4a1b9df643acff34cfab&client_secret=0KFHH9PZGitIIELlFYIcquCkGWTTRBVw", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            // Extract access token from response
            dynamic resp = JObject.Parse(response.Content);
            string token = resp.access_token;

            // get nin from session id
            var ninclient = new RestClient("https://api.signicat.io/identification/v2/sessions/" + sessionId);
            var ninrequest = new RestRequest(Method.GET);
            ninrequest.AddHeader("Authorization", "Bearer " + token);
            var body = @"";
            ninrequest.AddParameter("text/plain", body, ParameterType.RequestBody);
            IRestResponse ninresponse = ninclient.Execute(ninrequest);
            
            JObject ninresp = JObject.Parse(ninresponse.Content);
            var nin = ninresp["identity"]["nin"];

            // Basic lookup search
            var lookupClient = new RestClient("https://api.signicat.io/information/countries/NO/persons/?identityNumber=" + nin );
            var lookupRequest = new RestRequest(Method.GET);
            lookupRequest.AddHeader("Authorization", "Bearer " + token);
            IRestResponse lookupResponse = lookupClient.Execute(lookupRequest);
            
            JObject personInfo = JObject.Parse(lookupResponse.Content);
            Console.WriteLine(personInfo);
            
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