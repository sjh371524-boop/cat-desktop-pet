using System;
using System.Net;
using System.Net.Http;

internal static class WeatherNetworkProbe
{
    private static void Request(string label, string url)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(8);
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                Console.WriteLine(label + " HTTP " + (int)response.StatusCode);
                Console.WriteLine(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(label + " " + ex.GetType().Name + ": " + ex.Message);
            while (ex.InnerException != null) { ex = ex.InnerException; Console.WriteLine(ex.GetType().Name + ": " + ex.Message); }
        }
    }

    public static void Main()
    {
        Console.WriteLine("Default security protocols: " + ServicePointManager.SecurityProtocol);
        string url = "https://api.open-meteo.com/v1/forecast?latitude=31.2304&longitude=121.4737&current=weather_code&timezone=Asia%2FShanghai";
        Request("Existing weather transport", url);
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Request("TLS 1.2 weather transport", url);
        Request("TLS 1.2 optional geocoding", "https://geocoding-api.open-meteo.com/v1/search?name=Shanghai&count=1&language=zh&format=json");
    }
}
