using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BranchReload2
{
    public class RequestActionDispatch
    {
        public static async Task<string> Dispatch(string username, string repo, string token, RequestSendData rsd)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.everest-preview+json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token",token);

            Uri route = new Uri($"https://api.github.com/repos/{username}/{repo}/dispatches");
            
            HttpRequestMessage sendData = new HttpRequestMessage(HttpMethod.Post, route);
            sendData.Headers.UserAgent.Add(new("LandanParkerActionSubmitter", "v1.0"));
            
            var data = JsonConvert.SerializeObject(rsd);
            
            sendData.Content = new StringContent(data, Encoding.UTF8, "application/json");

            var hold = await client.SendAsync(sendData, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

            return await hold.Content.ReadAsStringAsync();
        }
    }
}