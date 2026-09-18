using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CodexCat
{
    internal static class WeatherVerifier
    {
        private const string Valid = "{\"current\":{\"weather_code\":3,\"time\":\"2026-09-03T14:15\"}}";
        internal static void Run()
        {
            AppSettings settings = new AppSettings();
            DateTime now = DateTime.UtcNow;
            foreach (int code in new int[] { 0,1,2,3,45,48,51,53,55,56,57,61,63,65,66,67,71,73,75,77,80,81,82,85,86,95,96,99 })
                if (WeatherService.DescribeCode(code) == null) throw new Exception("Missing weather code mapping.");
            foreach (string bad in new string[] { "{}", "{\"current\":{}}", "{\"current\":{\"weather_code\":null}}", "{\"current\":{\"weather_code\":999,\"time\":\"x\"}}" })
            {
                bool rejected = false;
                try { WeatherService.Parse(bad, settings, now); } catch (FormatException) { rejected = true; }
                if (!rejected) throw new Exception("Invalid weather must not appear as sunny.");
            }
            int calls = 0;
            var completion = new TaskCompletionSource<string>();
            var service = new WeatherService(settings, url => { calls++; return completion.Task; }, () => now, null);
            Task first = service.RefreshIfNeededAsync();
            Task second = service.RefreshIfNeededAsync();
            if (calls != 1 || first != second) throw new Exception("Concurrent refreshes must share one request.");
            completion.SetResult(Valid);
            first.GetAwaiter().GetResult();
            service.RefreshIfNeededAsync().GetAwaiter().GetResult();
            if (calls != 1 || service.CurrentDisplayInfo.LocationWeatherLine != "中国－上海市－阴") throw new Exception("Fresh cache is not displayed immediately.");

            Func<string, Task<string>> fail = url => { calls++; var t = new TaskCompletionSource<string>(); t.SetException(new HttpRequestException("test offline")); return t.Task; };
            service = new WeatherService(settings, fail, () => now, null);
            service.RefreshIfNeededAsync().GetAwaiter().GetResult();
            if (!service.CurrentDisplayInfo.LocationWeatherLine.Contains("暂不可用")) throw new Exception("Failure must not leave an infinite loading state.");
            int before = calls;
            service.RefreshIfNeededAsync().GetAwaiter().GetResult();
            if (before != calls) throw new Exception("Retry backoff is missing.");

            string path = Path.Combine(AppPaths.Root, "tests", "weather-cache-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                service = new WeatherService(settings, url => Task.FromResult(Valid), () => now, path);
                service.RefreshIfNeededAsync().GetAwaiter().GetResult();
                service = new WeatherService(settings, fail, () => now, path);
                if (!service.HasWeather) throw new Exception("Restart must restore the cache immediately.");
                now = now.AddMinutes(16);
                service.RefreshIfNeededAsync().GetAwaiter().GetResult();
                if (!service.CurrentDisplayInfo.LocationWeatherLine.Contains("阴（缓存）")) throw new Exception("Offline stale weather must be labeled.");
                var another = new AppSettings { Latitude = 40 };
                if (new WeatherService(another, fail, () => now, path).HasWeather) throw new Exception("A cache from another location must not be used.");
                now = now.AddDays(2);
                if (new WeatherService(settings, fail, () => now, path).HasWeather) throw new Exception("Expired weather must not be shown as current.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        internal static void RunLive()
        {
            var service = new WeatherService(AppSettings.Load());
            service.RefreshIfNeededAsync(true).GetAwaiter().GetResult();
            if (service.LastError != null || !service.HasWeather) throw new Exception(service.LastError ?? "No weather received.");
            Directory.CreateDirectory(Path.Combine(AppPaths.Root, "tests"));
            File.WriteAllText(Path.Combine(AppPaths.Root, "tests", "weather-live.txt"),
                DateTime.Now.ToString("o") + "\n" + service.CurrentDisplayText + "\n" + service.CurrentDisplayInfo.Details);
        }
    }
}
