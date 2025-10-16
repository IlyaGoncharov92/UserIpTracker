using System.Net.Http.Json;
using System.Threading.Channels;

// TODO: код почти полностью написан GPT, я почти не радактировал его.

record ConnectionDto(int UserId, string Ip);

class Program
{
    static readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static async Task Main()
    {
        await ClearDb();

        var apiUrl = "http://localhost:5047/api/connections";
        int userCount = 10_000;
        int ipsPerUser = 100;
        int testDurationSeconds = 30;

        var requests = Enumerable.Range(1, userCount)
            .SelectMany(userId =>
                Enumerable.Range(1, ipsPerUser)
                    .Select(ipIndex =>
                    {
                        int a = (userId + ipIndex) % 255;
                        int b = (userId * 3 + ipIndex * 7) % 255;
                        int c = (userId * 5 + ipIndex * 11) % 255;
                        int d = (userId * 13 + ipIndex * 17) % 255;

                        string ip = $"{a}.{b}.{c}.{d}";
                        return new ConnectionDto(userId, ip);
                    }));

        Console.WriteLine($"All combinations: {userCount * ipsPerUser:N0}");

        var startTime = DateTime.UtcNow;
        int sent = 0, errors = 0;

        // Канал для конкурентной отправки
        var channel = Channel.CreateBounded<ConnectionDto>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // Producer: записывает запросы в канал
        _ = Task.Run(async () =>
        {
            foreach (var req in requests)
            {
                if ((DateTime.UtcNow - startTime).TotalSeconds > testDurationSeconds)
                    break;

                await channel.Writer.WriteAsync(req);
            }

            channel.Writer.Complete();
        });

        // Consumer: одновременно обрабатывает запросы
        var consumers = Enumerable.Range(0, Environment.ProcessorCount * 4)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var req in channel.Reader.ReadAllAsync())
                {
                    try
                    {
                        var response = await httpClient.PostAsJsonAsync(apiUrl, req);
                        if (!response.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref errors);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }

                    Interlocked.Increment(ref sent);
                }
            })).ToArray();

        await Task.WhenAll(consumers);

        var elapsed = DateTime.UtcNow - startTime;

        var countNew = await GetCount();
        Console.WriteLine($"Send http: {sent:N0}");
        Console.WriteLine($"Error: {errors:N0}");
        Console.WriteLine($"Time: {elapsed.TotalSeconds:F1} sec");
        Console.WriteLine($"Speed: {sent / elapsed.TotalSeconds:F0} req/sec");
        Console.WriteLine($"Number of new records added to DB: {countNew:N0}");
    }
    
    static async Task ClearDb()
    {
        var apiUrl = "http://localhost:5047/api/connections/clear";
        var res = await httpClient.PostAsync(apiUrl, null);
        res.EnsureSuccessStatusCode();
    }

    static async Task<int> GetCount()
    {
        var apiUrl = "http://localhost:5047/api/connections/count";
        var res = await httpClient.PostAsync(apiUrl, null);
        res.EnsureSuccessStatusCode();
        var content = await res.Content.ReadAsStringAsync();
        return int.Parse(content);
    }
}

