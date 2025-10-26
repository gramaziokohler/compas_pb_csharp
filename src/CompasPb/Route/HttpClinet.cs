
namespace CompasPb.Route
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    public class RouteHttpClient
    {
        private readonly HttpClient httpClient;
        private readonly string serverUrl;

        public RouteHttpClient(string serverUrl)
        {
            this.httpClient = new HttpClient();
            this.serverUrl = serverUrl ?? throw new ArgumentNullException(nameof(serverUrl));
            Console.WriteLine($"HttpClient initialized with server URL: {this.serverUrl}");
        }

        // Test the connection to the server
        public async Task TestConnection()
        {
            try
            {
                var response = await this.httpClient.GetAsync(this.serverUrl);
                bool isSuccess = response.IsSuccessStatusCode;
                if (isSuccess)
                {
                    Console.WriteLine($"Response status: {response.StatusCode}");
                }
                else
                {
                    Console.WriteLine(
                        $"Failed to connect to the server. Status code: {response.StatusCode}");
                }
            }
            catch (HttpRequestException e) // Fixed: HttpRequestException instead of HttpIOException
            {
                Console.WriteLine($"HTTP request error: {e.Message}");
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine($"Request timeout: {e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"An unexpected error occurred: {e.Message}");
            }
        }

        public async Task<byte[]?> GetData()
        {
            string url = $"{this.serverUrl}/sender";
            try
            {
                using (var response = await this.httpClient.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] responseContent = await response.Content.ReadAsByteArrayAsync();
                        return responseContent;
                    }
                    else
                    {
                        Console.WriteLine($"Error response: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred while getting data: {e.Message}");
                return null;
            }
        }

        public async Task PostData(byte[] data)
        {
            string url = $"{this.serverUrl}/receiver";
            try
            {
                using (var content = new ByteArrayContent(data))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
                    var response = await this.httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Data posted successfully.");
                    }
                    else
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Error response: {errorContent}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred while posting data: {e.Message}");
            }
        }

        // Dispose method to clean up resources
        public void Dispose()
        {
            this.httpClient?.Dispose();
        }
    }
}
