using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace Esri.APL.MilesPerDollar {
    internal class VehiclesPaneViewModel : DockPane {
        private const string _dockPaneID = "Esri_APL_MilesPerDollar_VehiclesPane";

        protected VehiclesPaneViewModel() { }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show() {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Vehicles";
        public string Heading {
            get { return _heading; }
            set {
                SetProperty(ref _heading, value, () => Heading);
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class VehiclesPane_ShowButton : Button {
        protected override void OnClick() {
            VehiclesPaneViewModel.Show();
        }
    }
}
