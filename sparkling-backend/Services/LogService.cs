using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Services;

public class LogService: ILogService
{
    // SSE: list of connected clients
    private readonly ConcurrentDictionary<HttpResponse, int> _sseClients = new();

    // SSE: broadcast helper
    public void AddClient(HttpResponse response)
    {
        _sseClients.TryAdd(response, 0);
    }

    public void RemoveClient(HttpResponse response)
    {
        _sseClients.TryRemove(response, out _);
    }

    public async void Broadcast(Log log)
    {
        try
        {
            var data = $"data: {JsonSerializer.Serialize(log)}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);
            foreach (var resp in _sseClients.Keys)
            {
                try
                {
                    await resp.Body.WriteAsync(bytes);
                    await resp.Body.FlushAsync();
                }
                catch
                {
                    _sseClients.Remove(resp, out _);
                }
            }
        }
        catch
        {
            // async void Broadcast should not throw exceptions
        }
    }
}

public interface ILogService
{
    void AddClient(HttpResponse response);
    void RemoveClient(HttpResponse response);
    void Broadcast(Log logMessage);
}