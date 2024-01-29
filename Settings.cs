using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GU_Exchange
{
    internal class Settings
    {
        #region Static Fields.
        private static readonly Dictionary<string, string> s_settings = new Dictionary<string, string>();
        private static bool SettingsLoaded = false;
        #endregion

        #region Load/Save Settings
        /// <summary>
        /// Load GU Exchange settings from the disk.
        /// </summary>
        public static void LoadSettings()
        {
            try
            {
                StreamReader sr = new StreamReader("settings.txt");
                string? line = sr.ReadLine();
                //Continue to read until you reach end of file
                while (line != null)
                {
                    if (line.Contains("="))
                    {
                        string[] setting = line.Split("=");
                        s_settings[setting[0]] = setting[1];
                    }
                    //Read the next line
                    line = sr.ReadLine();
                }
                //close the file
                sr.Close();
            }
            catch (FileNotFoundException)
            {
                // Nothing to load.
            }
        }

        /// <summary>
        /// Save GU Exchange settings to the disk.
        /// </summary>
        /// <returns></returns>
        public static bool SaveSettings()
        {
            try
            {
                StreamWriter sw = new StreamWriter("settings.txt");
                foreach (string key in s_settings.Keys)
                {
                    sw.WriteLine($"{key}={s_settings[key]}");
                }
                sw.Close();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception: {e.Message}");
                return false;
            }
            return true;
        }
        #endregion

        #region Getters/Setters.
        public static int GetApolloID()
        {
            if (!SettingsLoaded)
            {
                LoadSettings();
                SettingsLoaded = true;
            }
            try
            {
                return int.Parse(s_settings["apolloid"]);
            }
            catch (KeyNotFoundException)
            {
                return -1;
            }
        }

        public static void SetApolloID(int apolloID)
        {
            s_settings["apolloid"] = apolloID.ToString();
        }

        public static int GetServerPort()
        {
            if (!SettingsLoaded)
            {
                LoadSettings();
                SettingsLoaded = true;
            }
            try
            {
                return int.Parse(s_settings["port"]);
            }
            catch (KeyNotFoundException)
            {
                SetServerPort(8080);
                SaveSettings();
                return 8080;
            }
        }

        public static void SetServerPort(int port)
        {
            s_settings["port"] = port.ToString();
        }

        public static string GetSetting(string setting)
        {
            if (!SettingsLoaded)
            {
                LoadSettings();
                SettingsLoaded = true;
            }
            try
            {
                return s_settings[setting];
            }
            catch (KeyNotFoundException)
            {
                return "";
            }
        }

        public static void SetSetting(string setting, string value)
        {
            s_settings[setting] = value;
        }
        #endregion
    }
}
