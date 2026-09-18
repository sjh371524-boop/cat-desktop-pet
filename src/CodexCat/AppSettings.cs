using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexCat
{
    internal sealed class AppSettings
    {
        public string PetName { get; set; }
        public double PetScale { get; set; }
        public double IdleMinutes { get; set; }
        public double IdleAnimation1Seconds { get; set; }
        public int DefaultIdlesBeforeSleep { get; set; }
        public bool KeepAwake { get; set; }
        public int WeatherRefreshMinutes { get; set; }
        public string LocationName { get; set; }
        public string CountryName { get; set; }
        public string RegionName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Timezone { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool StartNearBottomRight { get; set; }

        public AppSettings()
        {
            PetName = "猫猫";
            PetScale = 1.0;
            IdleMinutes = 5.0;
            IdleAnimation1Seconds = 60.0;
            DefaultIdlesBeforeSleep = 5;
            KeepAwake = false;
            WeatherRefreshMinutes = 15;
            LocationName = "上海";
            CountryName = "中国";
            RegionName = "上海市";
            Latitude = 31.2304;
            Longitude = 121.4737;
            Timezone = "Asia/Shanghai";
            AlwaysOnTop = true;
            StartNearBottomRight = true;
        }

        public static AppSettings Load()
        {
            string localPath = Path.Combine(AppPaths.ConfigDirectory, "settings.local.json");
            string normalPath = Path.Combine(AppPaths.ConfigDirectory, "settings.json");
            string path = File.Exists(localPath) ? localPath : normalPath;

            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                AppSettings settings = new JavaScriptSerializer().Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void SaveLocal()
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            string path = Path.Combine(AppPaths.ConfigDirectory, "settings.local.json");
            string json = new JavaScriptSerializer().Serialize(this);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
    }
}
