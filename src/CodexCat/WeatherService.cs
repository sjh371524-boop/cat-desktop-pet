using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexCat
{
    internal enum WeatherIconKind { Loading, Sunny, PartlyCloudy, Cloudy, Fog, Rain, Snow, Thunder }
    internal sealed class WeatherDisplayInfo
    {
        public string LocationWeatherLine { get; set; }
        public string DateLine { get; set; }
        public WeatherIconKind IconKind { get; set; }
        public string Details { get; set; }
    }
    internal sealed class WeatherSnapshot
    {
        public string Condition { get; set; }
        public WeatherIconKind IconKind { get; set; }
        public int WeatherCode { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string ObservationTime { get; set; }
    }
    internal sealed class WeatherService
    {
        private static readonly HttpClient Http = CreateHttpClient();
        private readonly AppSettings settings;
        private readonly Func<string, Task<string>> download;
        private readonly Func<DateTime> utcNow;
        private readonly string cachePath;
        private readonly object gate = new object();
        private WeatherSnapshot cached;
        private Task inFlight;
        private DateTime retryAfter;
        internal string LastError { get; private set; }
        internal bool HasWeather { get { return cached != null; } }

        public WeatherService(AppSettings settings) : this(settings, Http.GetStringAsync,
            () => DateTime.UtcNow, Path.Combine(AppPaths.ConfigDirectory, "weather-cache.json")) { }
        internal WeatherService(AppSettings settings, Func<string, Task<string>> download, Func<DateTime> utcNow, string cachePath)
        {
            this.settings = settings; this.download = download; this.utcNow = utcNow; this.cachePath = cachePath;
            LoadCache();
        }
        public WeatherDisplayInfo CurrentDisplayInfo
        {
            get
            {
                WeatherSnapshot snapshot = cached;
                bool stale = snapshot != null && utcNow() - snapshot.UpdatedAt.ToUniversalTime() >= RefreshAge;
                string condition = snapshot == null ? (LastError == null ? "天气加载中" : "天气暂不可用") :
                    snapshot.Condition + (stale ? "（缓存）" : "");
                return new WeatherDisplayInfo
                {
                    LocationWeatherLine = FirstNonEmpty(settings.CountryName, "中国") + "－" +
                        FirstNonEmpty(settings.RegionName, settings.LocationName, "当前位置") + "－" + condition,
                    DateLine = DateTime.Now.ToString("yyyy年M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN")),
                    IconKind = snapshot == null ? WeatherIconKind.Loading : snapshot.IconKind,
                    Details = snapshot == null ? (LastError ?? "正在获取当前天气") :
                        "天气来源：Open-Meteo\n数据时间：" + snapshot.ObservationTime +
                        "\n获取时间：" + snapshot.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
                        (stale ? "\n当前显示上次成功的数据，后台正在重试。" : "")
                };
            }
        }
        public string CurrentDisplayText { get { return CurrentDisplayInfo.LocationWeatherLine + "\n" + CurrentDisplayInfo.DateLine; } }
        private TimeSpan RefreshAge { get { return TimeSpan.FromMinutes(Math.Max(1, settings.WeatherRefreshMinutes)); } }
        public Task RefreshIfNeededAsync(bool force = false)
        {
            lock (gate)
            {
                if (inFlight != null && !inFlight.IsCompleted) return inFlight;
                if (!force && (utcNow() < retryAfter || (cached != null && utcNow() - cached.UpdatedAt.ToUniversalTime() < RefreshAge))) return Task.FromResult(0);
                inFlight = RefreshCoreAsync();
                return inFlight;
            }
        }
        private async Task RefreshCoreAsync()
        {
            string url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=weather_code&timezone={2}",
                settings.Latitude, settings.Longitude, Uri.EscapeDataString(settings.Timezone ?? "auto"));
            try
            {
                // Location names are already correct. Do not serialize a redundant
                // geocoding request ahead of every weather request.
                string json = await download(url).ConfigureAwait(false);
                cached = Parse(json, settings, utcNow());
                LastError = null; retryAfter = DateTime.MinValue;
                SaveCache();
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                LastError = "天气获取失败：" + ex.Message;
                retryAfter = utcNow().AddSeconds(30);
                WriteDiagnostic(LastError);
            }
        }
        internal static WeatherSnapshot Parse(string json, AppSettings settings, DateTime fetchedUtc)
        {
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            object obj;
            if (root == null || !root.TryGetValue("current", out obj)) throw new FormatException("天气响应缺少 current 数据。");
            var current = obj as Dictionary<string, object>;
            if (current == null || !current.TryGetValue("weather_code", out obj) || obj == null) throw new FormatException("天气响应缺少 weather_code，不能将空值当作晴天。");
            int code = Convert.ToInt32(obj, CultureInfo.InvariantCulture);
            string condition = DescribeCode(code);
            if (condition == null) throw new FormatException("无法识别天气代码：" + code);
            object time;
            if (!current.TryGetValue("time", out time) || time == null) throw new FormatException("天气响应缺少数据时间。");
            return new WeatherSnapshot
            {
                Condition = condition, IconKind = GetIconKind(code), WeatherCode = code,
                Country = settings.CountryName, Region = settings.RegionName, Latitude = settings.Latitude, Longitude = settings.Longitude,
                UpdatedAt = fetchedUtc.ToUniversalTime(), ObservationTime = Convert.ToString(time, CultureInfo.InvariantCulture)
            };
        }
        private void LoadCache()
        {
            try
            {
                if (cachePath == null || !File.Exists(cachePath)) return;
                var value = new JavaScriptSerializer().Deserialize<WeatherSnapshot>(File.ReadAllText(cachePath));
                if (value == null || DescribeCode(value.WeatherCode) == null || string.IsNullOrEmpty(value.ObservationTime)
                    || Math.Abs(value.Latitude - settings.Latitude) > .00001 || Math.Abs(value.Longitude - settings.Longitude) > .00001
                    || value.UpdatedAt.ToUniversalTime() > utcNow().AddMinutes(5)
                    || utcNow() - value.UpdatedAt.ToUniversalTime() > TimeSpan.FromDays(1)) return;
                value.Condition = DescribeCode(value.WeatherCode); value.IconKind = GetIconKind(value.WeatherCode);
                cached = value;
            }
            catch { /* A malformed cache must not prevent a live request. */ }
        }
        private void SaveCache()
        {
            if (cachePath == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(cachePath, new JavaScriptSerializer().Serialize(cached));
            }
            catch (Exception ex) { WriteDiagnostic("天气已获取，但本地缓存写入失败：" + ex.Message); }
        }
        private void WriteDiagnostic(string error)
        {
            if (cachePath == null) return;
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(cachePath), "weather-status.log"), utcNow().ToString("o") + " " + error); }
            catch { }
        }
        private static string FirstNonEmpty(params string[] values)
        { foreach (string value in values) if (!string.IsNullOrWhiteSpace(value)) return value.Trim(); return ""; }
        private static HttpClient CreateHttpClient()
        {
            // Reproduced old .NET defaults: SSL3/TLS1 handshake fails; TLS1.2
            // returns HTTP200. Keep certificate validation and OS settings intact.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexCat/1.1");
            return client;
        }
        internal static WeatherIconKind GetIconKind(int code)
        {
            if (code == 0) return WeatherIconKind.Sunny;
            if (code == 1 || code == 2) return WeatherIconKind.PartlyCloudy;
            if (code == 3) return WeatherIconKind.Cloudy;
            if (code == 45 || code == 48) return WeatherIconKind.Fog;
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return WeatherIconKind.Rain;
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return WeatherIconKind.Snow;
            return WeatherIconKind.Thunder;
        }
        internal static string DescribeCode(int code)
        {
            switch (code)
            {
                case 0: return "晴"; case 1: return "大致晴朗"; case 2: return "多云"; case 3: return "阴";
                case 45: case 48: return "雾";
                case 51: case 53: case 55: return "毛毛雨";
                case 56: case 57: case 66: case 67: return "冻雨";
                case 61: case 63: case 65: return "雨";
                case 71: case 73: case 75: case 77: return "雪";
                case 80: case 81: case 82: return "阵雨";
                case 85: case 86: return "阵雪";
                case 95: case 96: case 99: return "雷雨";
                default: return null;
            }
        }
    }
}
