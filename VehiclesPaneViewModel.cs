using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Reflection;
using System.IO;

namespace Esri.APL.MilesPerDollar {
    internal class VehiclesPaneViewModel : DockPane {
        // Thread locking objects
        protected object _lockXmlYears = new object();

        #region Model variables and properties

        private XDocument _xmlAllVehicles;

        private ObservableCollection<String> _vehicleYears;
        //private readonly ReadOnlyObservableCollection<String> _readonlyVehicleYears;

        public ObservableCollection<String> VehicleYears {
            get { return _vehicleYears; }
            set { SetProperty(ref _vehicleYears, value); }
        }

        #endregion

        #region CTOR & Initialization
        protected VehiclesPaneViewModel() {
            // Set up necessary defaults
            //_readonlyVehicleYears = new ReadOnlyObservableCollection<String>(_vehicleYears);
            //BindingOperations.EnableCollectionSynchronization(_readonlyVehicleYears, _lockXmlYears);

        }

        protected override Task InitializeAsync() {
            // Read vehicles data
            Uri uri = new Uri("pack://application:,,,/Esri.APL.MilesPerDollar;component/Resources/FE_1984-2018.xml");
            System.IO.Stream stIn = System.Windows.Application.GetResourceStream(uri).Stream;
            _xmlAllVehicles = XDocument.Load(stIn);

            var qryYears = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                           orderby vehicle.Attribute("year").Value
                           select vehicle.Attribute("year").Value;
            VehicleYears = new ObservableCollection<String>(qryYears.Distinct());
            return base.InitializeAsync();
        }
        #endregion

        #region Dockpane Stuff
        private const string _dockPaneID = "Esri_APL_MilesPerDollar_VehiclesPane";

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

    #endregion
}
