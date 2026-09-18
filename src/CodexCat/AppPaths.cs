using System;
using System.IO;

namespace CodexCat
{
    internal static class AppPaths
    {
        public static readonly string Root = FindRoot();
        public static readonly string ConfigDirectory = Path.Combine(Root, "config");
        public static readonly string CharacterDirectory = Path.Combine(Root, "assets", "character");
        public static readonly string UiDirectory = Path.Combine(Root, "assets", "ui");

        private static string FindRoot()
        {
            string current = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(current, "config", "settings.json")))
            {
                return current;
            }

            string executable = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(executable, "config", "settings.json")))
            {
                return executable;
            }

            return current;
        }
    }
}
