﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Esri.APL.MilesPerDollar.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://maps.esri.com/md/MilesPerDollar/com/esri/apl/mpd_mvc/assets/data/States2P" +
            "ADD.xml")]
        public string PADDZonesUrl {
            get {
                return ((string)(this["PADDZonesUrl"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://maps.esri.com/md/MilesPerDollar/com/esri/apl/mpd_mvc/assets/data/vehicles" +
            ".xml")]
        public string VehicleInfoUrl {
            get {
                return ((string)(this["VehicleInfoUrl"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://maps.esri.com/md/MilesPerDollar/com/esri/apl/mpd_mvc/assets/data/PET_PRI_" +
            "GND_A_EPM0_PTE_DPGAL_W.xls")]
        public string FuelCostUrl {
            get {
                return ((string)(this["FuelCostUrl"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://cirque.esri.com/MilesPerDollar/PET_PRI_GND_A_EPM0_PTE_DPGAL_W.xls")]
        public string FuelCostUrl_debug {
            get {
                return ((string)(this["FuelCostUrl_debug"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://sampleserver3.arcgisonline.com/ArcGIS/rest/services/Network/USA/MapServer" +
            "/121/query")]
        public string QryPointToState {
            get {
                return ((string)(this["QryPointToState"]));
            }
        }
    }
}