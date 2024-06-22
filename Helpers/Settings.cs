using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.RightsManagement;

namespace GU_Exchange.Helpers
{
    internal class Settings
    {
        #region Static Fields.
        private static string folderConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GU-Exchange");

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
                StreamReader sr = new StreamReader(Path.Combine(GetConfigFolder(), "settings.txt"));
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
                StreamWriter sw = new StreamWriter(Path.Combine(GetConfigFolder(), "settings.txt"));
                foreach (string key in s_settings.Keys)
                {
                    sw.WriteLine($"{key}={s_settings[key]}");
                }
                sw.Close();
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to save settings. {e.Message}: {e.StackTrace}");
                return false;
            }
            return true;
        }
        #endregion

        #region Getters/Setters.
        /// <summary>
        /// Get the config folder 
        /// </summary>
        /// <returns></returns>
        public static string GetConfigFolder()
        {
            if (!Directory.Exists(folderConfig))
            {
                Directory.CreateDirectory(folderConfig);
            }
            return folderConfig;
        }

        /// <summary>
        /// Get the apolloID linked to GU Exchange.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Set the apolloID linked to GU Exchange.
        /// </summary>
        /// <param name="apolloID"></param>
        public static void SetApolloID(int apolloID)
        {
            s_settings["apolloid"] = apolloID.ToString();
        }

        /// <summary>
        /// Get the preferred port for running the local webserver for signing messages.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Set the preferred port for running the local webserver for signing messages.
        /// </summary>
        /// <param name="port"></param>
        public static void SetServerPort(int port)
        {
            s_settings["port"] = port.ToString();
        }

        /// <summary>
        /// Get the limit value above which the user will be warned before a currency transfer.
        /// </summary>
        /// <returns></returns>
        public static decimal GetTransferWarningLimit()
        {
            if (!SettingsLoaded)
            {
                LoadSettings();
                SettingsLoaded = true;
            }
            try
            {
                return decimal.Parse(s_settings["transferlimit"]);
            }
            catch (KeyNotFoundException)
            {
                return decimal.Parse("100");
            }
        }

        /// <summary>
        /// Set the limit value above which the user will be warned before a currency transfer.
        /// </summary>
        /// <param name="limit"></param>
        public static void SetTransferWarningLimit(decimal limit)
        {
            s_settings["transferlimit"] = limit.ToString();
        }

        /// <summary>
        /// Get the current value for a setting.
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Update the specified setting.
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="value"></param>
        public static void SetSetting(string setting, string value)
        {
            s_settings[setting] = value;
        }
        #endregion
    }
}
