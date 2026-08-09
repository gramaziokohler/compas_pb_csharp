using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CompasPb.Data;
using Google.Protobuf;

namespace CompasPb.Route;

public class CompasPbHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private readonly ICompasPbSerializer _serializer;

    public CompasPbHttpClient(string serverUrl, ICompasPbSerializer? serializer = null)
    {
        _serverUrl = serverUrl ?? throw new ArgumentNullException(nameof(serverUrl));
        _httpClient = new HttpClient();
        _serializer = serializer ?? new CompasPbSerializer();
    }

    public async Task TestConnection()
    {
        try
        {
            var response = await _httpClient.GetAsync(_serverUrl);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Response status: {response.StatusCode}");
            }
            else
            {
                Console.WriteLine(
                    $"Failed to connect to the server. Status code: {response.StatusCode}"
                );
            }
        }
        catch (HttpRequestException e)
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

    /// <summary>
    /// Packs data and sends it to the /receiver endpoint.
    /// </summary>
    public async Task SendAsync(object? data)
    {
        byte[] bytes = _serializer.Pack(data);
        await PostData(bytes);
    }

    /// <summary>
    /// Receives raw bytes from /sender and unpacks dynamically — returns object?.
    /// </summary>
    public async Task<object?> ReceiveAsync()
    {
        byte[]? bytes = await GetData();
        return bytes == null ? null : _serializer.Unpack(bytes);
    }

    /// <summary>
    /// Receives raw bytes from /sender and unpacks as T.
    /// </summary>
    public async Task<T?> ReceiveAsync<T>()
        where T : class, IMessage<T>, new()
    {
        byte[]? bytes = await GetData();
        return bytes == null ? null : _serializer.Unpack<T>(bytes);
    }

    /// <summary>
    /// Low-level: POST raw protobuf bytes to /receiver.
    /// </summary>
    public async Task PostData(byte[] data)
    {
        string url = $"{_serverUrl}/receiver";
        try
        {
            using var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response: {errorContent}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred while posting data: {e.Message}");
        }
    }

    /// <summary>
    /// Low-level: GET raw protobuf bytes from /sender.
    /// </summary>
    public async Task<byte[]?> GetData()
    {
        string url = $"{_serverUrl}/sender";
        try
        {
            using var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            Console.WriteLine($"Error response: {response.StatusCode}");
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred while getting data: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
