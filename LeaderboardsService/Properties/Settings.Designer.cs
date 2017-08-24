﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.3.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        /// <summary>
        /// The version of the settings file format.
        /// </summary>
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The version of the settings file format.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1")]
        public int Version {
            get {
                return ((int)(this["Version"]));
            }
        }
        
        /// <summary>
        /// The product&apos;s application ID.
        /// </summary>
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The product\'s application ID.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("247080")]
        public uint AppId {
            get {
                return ((uint)(this["AppId"]));
            }
        }
        
        /// <summary>
        /// The minimum amount of time that should pass between each cycle.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The minimum amount of time that should pass between each cycle.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("00:02:00")]
        public global::System.TimeSpan UpdateInterval {
            get {
                return ((global::System.TimeSpan)(this["UpdateInterval"]));
            }
            set {
                this["UpdateInterval"] = value;
            }
        }
        
        /// <summary>
        /// The amount of time to wait after a cycle to perform garbage collection.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The amount of time to wait after a cycle to perform garbage collection.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("00:00:00")]
        public global::System.TimeSpan DelayBeforeGC {
            get {
                return ((global::System.TimeSpan)(this["DelayBeforeGC"]));
            }
            set {
                this["DelayBeforeGC"] = value;
            }
        }
        
        /// <summary>
        /// The user name used to log on to Steam.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The user name used to log on to Steam.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public string SteamUserName {
            get {
                return ((string)(this["SteamUserName"]));
            }
            set {
                this["SteamUserName"] = value;
            }
        }
        
        /// <summary>
        /// The password used to log on to Steam.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The password used to log on to Steam.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::toofz.EncryptedSecret SteamPassword {
            get {
                return ((global::toofz.EncryptedSecret)(this["SteamPassword"]));
            }
            set {
                this["SteamPassword"] = value;
            }
        }
        
        /// <summary>
        /// The connection string used to connect to the leaderboards database.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("The connection string used to connect to the leaderboards database.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::toofz.EncryptedSecret LeaderboardsConnectionString {
            get {
                return ((global::toofz.EncryptedSecret)(this["LeaderboardsConnectionString"]));
            }
            set {
                this["LeaderboardsConnectionString"] = value;
            }
        }
        
        /// <summary>
        /// An Application Insights instrumentation key.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Configuration.SettingsDescriptionAttribute("An Application Insights instrumentation key.")]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public string LeaderboardsInstrumentationKey {
            get {
                return ((string)(this["LeaderboardsInstrumentationKey"]));
            }
            set {
                this["LeaderboardsInstrumentationKey"] = value;
            }
        }
    }
}
