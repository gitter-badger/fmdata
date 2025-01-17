using RichardSzalay.MockHttp;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

// this is apparently necessary to work in appveyor / myget
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FMData.Rest.Tests
{
    /// <summary>
    /// Tests to ensure the proper authentication json is created.
    /// </summary>
    public class AuthenticationTests
    {
        [Fact(DisplayName = "Client is Unauthenticated Until Action Is Invoked")]
        public void DoesNotGetTokenOnConstructor()
        {
            using (var mockHttp = new MockHttpMessageHandler())
            {
                var server = "http://localhost/";
                var file = "test-file";
                var user = "unit";
                var pass = "test";

                // note the lack of slash here vs other tests to ensure the actual auth endpoint is correctly mocked/hit
                mockHttp.When($"{server}fmi/data/v1/databases/{file}/sessions")
                .Respond("application/json", DataApiResponses.SuccessfulAuthentication());

                mockHttp.When(HttpMethod.Delete, $"{server}fmi/data/v1/databases/{file}/sessions*")
                .Respond(HttpStatusCode.OK, "application/json", "");

                using (var fdc = new FileMakerRestClient(mockHttp.ToHttpClient(), server, file, user, pass))
                {
                    Assert.False(fdc.IsAuthenticated);
                }
            }
        }

        [Fact(DisplayName ="FMS 401 Should Result In Unauthorized Client")]
        public void DataCliet_ShouldHandle_401_From_Sessions()
        {
            var mockHttp = new MockHttpMessageHandler();

            var server = "http://localhost";
            var file = "test-file";
            var user = "unit";
            var pass = "test";

            mockHttp.When(HttpMethod.Post, $"{server}/fmi/data/v1/databases/{file}/sessions")
                .Respond(HttpStatusCode.Unauthorized, "application/json", DataApiResponses.Authentication401());

            mockHttp.When(HttpMethod.Delete, $"{server}/fmi/data/v1/databases/{file}/sessions*")
                .Respond(HttpStatusCode.OK, "application/json", "");

            using (var fdc = new FileMakerRestClient(mockHttp.ToHttpClient(), server, file, user, pass))
            {
                Assert.False(fdc.IsAuthenticated);
            }
        }

        [Fact(DisplayName ="Refresh Token Should Generate New Token")]
        public async Task RefreshToken_ShouldGet_NewToken()
        {
            var mockHttp = new MockHttpMessageHandler();

            var server = "http://localhost";
            var file = "test-file";
            var user = "unit";
            var pass = "test";

            mockHttp.When($"{server}/fmi/data/v1/databases/{file}/sessions")
                .Respond("application/json", DataApiResponses.SuccessfulAuthentication("someOtherToken"));

            mockHttp.When(HttpMethod.Delete, $"{server}/fmi/data/v1/databases/{file}/sessions*")
                .Respond(HttpStatusCode.OK, "application/json", "");

            using (var fdc = new FileMakerRestClient(mockHttp.ToHttpClient(), server, file, user, pass))
            {
                var response = await fdc.RefreshTokenAsync("integration", "test");
                Assert.Equal("someOtherToken", response.Response.Token);
            }
        }

        [Theory(DisplayName = "Refresh Token Requires Username and Password")]
        [InlineData("", "test")]
        [InlineData("integration", "")]
        public async Task RefreshToken_Requires_AllParameters(string user, string pass)
        {
            var mockHttp = new MockHttpMessageHandler();

            var server = "http://localhost";
            var file = "test-file";

            mockHttp.When($"{server}/fmi/data/v1/databases/{file}/sessions")
                .Respond("application/json", DataApiResponses.SuccessfulAuthentication("someOtherToken"));

            mockHttp.When(HttpMethod.Delete, $"{server}/fmi/data/v1/databases/{file}/sessions*")
                .Respond(HttpStatusCode.OK, "application/json", "");

            // pass in actual values here since we DON'T want this to blow up on constructor 
            using (var fdc = new FileMakerRestClient(mockHttp.ToHttpClient(), server, file, "user", "pass"))
            {
                await Assert.ThrowsAsync<ArgumentException>(async () => await fdc.RefreshTokenAsync(user, pass));
            }
        }
    }
}