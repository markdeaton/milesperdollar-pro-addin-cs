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
using System.Windows;
using System.Globalization;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace Esri.APL.MilesPerDollar {
    internal class VehiclesPaneViewModel : DockPane {
        // Thread locking objects
        protected object _lockXmlYears = new object();

        #region Model variables and properties

        private XDocument _xmlAllVehicles;
        private ObservableCollection<Vehicle> _selectedVehicles = new ObservableCollection<Vehicle>();

        private ObservableCollection<String> _vehicleYears, _vehicleMakes, _vehicleModels, _vehicleTypes;
        // Since we're updating the dropdowns on the UI thread, no need to use the convoluted
        // read-only sync pattern shown in some of the samples.
        //private readonly ReadOnlyObservableCollection<String> _readonlyVehicleYears;

        private String _selectedVehicleYear, _selectedVehicleMake, _selectedVehicleModel, _selectedVehicleType;
        private XElement _selectedVehicle;

        public ObservableCollection<String> VehicleYears {
            get { return _vehicleYears; }
            set { SetProperty(ref _vehicleYears, value); }
        }

        public ObservableCollection<string> VehicleMakes {
            get { return _vehicleMakes; }
            set { SetProperty(ref _vehicleMakes, value); }
        }

        public ObservableCollection<string> VehicleModels {
            get { return _vehicleModels; }
            set { SetProperty( ref _vehicleModels, value); }
        }

        public ObservableCollection<string> VehicleTypes {
            get { return _vehicleTypes; }
            set { SetProperty( ref _vehicleTypes, value); }
        }

        public string SelectedVehicleYear {
            get { return _selectedVehicleYear; }
            set {
                SetProperty( ref _selectedVehicleYear, value);
                SelectedVehicleMake = SelectedVehicleModel = SelectedVehicleType = null;
                GetVehicleMakes();
            }
        }

        public string SelectedVehicleMake {
            get { return _selectedVehicleMake; }
            set {
                SetProperty(ref _selectedVehicleMake, value);
                SelectedVehicleModel = SelectedVehicleType = null;
                GetVehicleModels();
            }
        }

        public string SelectedVehicleModel {
            get { return _selectedVehicleModel; }
            set {
                SetProperty(ref _selectedVehicleModel, value);
                SelectedVehicleType = null;
                GetVehicleTypes();
            }
        }

        public string SelectedVehicleType {
            get { return _selectedVehicleType; }
            set {
                SetProperty(ref _selectedVehicleType, value);
                IEnumerable<XElement> xVehicles = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                                   where vehicle.Attribute("year").Value == SelectedVehicleYear &&
                                         vehicle.Attribute("make").Value == SelectedVehicleMake &&
                                         vehicle.Attribute("model").Value == SelectedVehicleModel &&
                                         vehicle.Attribute("engine").Value == SelectedVehicleType
                                   select vehicle;
                XElement xVehicle = xVehicles.Count() > 0 ? xVehicles.First() : null;
                SelectedVehicle = xVehicle;
            }
        }

        public XElement SelectedVehicle {
            get { return _selectedVehicle; }
            set {
                SetProperty(ref _selectedVehicle, value);
            }
        }

        public ObservableCollection<Vehicle> SelectedVehicles {
            get { return _selectedVehicles; }
            set {
                SetProperty(ref _selectedVehicles, value);
            }
        }

        private void ReadVehicleData() {
            // Read vehicles data
            Uri uri = new Uri("pack://application:,,,/Esri.APL.MilesPerDollar;component/Resources/FE_1984-2018.xml");
            System.IO.Stream stIn = System.Windows.Application.GetResourceStream(uri).Stream;
            _xmlAllVehicles = XDocument.Load(stIn);
        }
        private void GetVehicleYears() {
            var qryYears = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                           orderby vehicle.Attribute("year").Value
                           select vehicle.Attribute("year").Value;
            VehicleYears = new ObservableCollection<String>(qryYears.Distinct());
        }
        private void GetVehicleMakes() {
            var qryMakes = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                           where vehicle.Attribute("year").Value == SelectedVehicleYear
                           orderby vehicle.Attribute("make").Value
                           select vehicle.Attribute("make").Value;
            VehicleMakes = new ObservableCollection<string>(qryMakes.Distinct());
        }
        private void GetVehicleModels() {
            var qryModels = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                            where vehicle.Attribute("year").Value == SelectedVehicleYear &&
                                  vehicle.Attribute("make").Value == SelectedVehicleMake
                            orderby vehicle.Attribute("model").Value
                            select vehicle.Attribute("model").Value;
            VehicleModels = new ObservableCollection<string>(qryModels.Distinct());                            
        }
        private void GetVehicleTypes() {
            var qryTypes = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                            where vehicle.Attribute("year").Value == SelectedVehicleYear &&
                                  vehicle.Attribute("make").Value == SelectedVehicleMake &&
                                  vehicle.Attribute("model").Value == SelectedVehicleModel
                            orderby vehicle.Attribute("engine").Value
                            select vehicle.Attribute("engine").Value;
            VehicleTypes = new ObservableCollection<string>(qryTypes.Distinct());                            
        }

        #endregion

        #region CTOR & Initialization
        protected VehiclesPaneViewModel() {
            // Set up necessary defaults
            //_readonlyVehicleYears = new ReadOnlyObservableCollection<String>(_vehicleYears);
            //BindingOperations.EnableCollectionSynchronization(_readonlyVehicleYears, _lockXmlYears);
            _addSelectedVehicleCommand = new RelayCommand(() => AddSelectedVehicle(), () => CanAddSelectedVehicle());
            _removeSelectedVehicleCommand = new RelayCommand(() => RemoveSelectedVehicle(), () => true);
        }

        protected override Task InitializeAsync() {
            ReadVehicleData();
            GetVehicleYears();
            return base.InitializeAsync();
        }
        #endregion

        #region Add/Remove Vehicle commands

        public ICommand AddSelectedVehicleCommand => _addSelectedVehicleCommand;
        private ICommand _addSelectedVehicleCommand;
        public ICommand RemoveSelectedVehicleCommand => _removeSelectedVehicleCommand;
        private ICommand _removeSelectedVehicleCommand;

        public bool CanAddSelectedVehicle() {
            bool vehicleSelected = SelectedVehicle != null;
            bool tooManyVehiclesAlreadyChosen = SelectedVehicles.Count >= 2;
            return vehicleSelected && !tooManyVehiclesAlreadyChosen;
        }
        private void AddSelectedVehicle() {
            System.Diagnostics.Debug.WriteLine("AddSelectedVehicle");
            SelectedVehicles.Add(new Vehicle(SelectedVehicle));
        }
        private void RemoveSelectedVehicle() {
            System.Diagnostics.Debug.WriteLine("RemoveSelectedVehicle");
        }
        #endregion

        #region Dockpane Plumbing

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
        #endregion
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class VehiclesPane_ShowButton : Button {
        protected override void OnClick() {
            VehiclesPaneViewModel.Show();
        }
    }

    #region Value Converters

    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToIsVisibleConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            System.Diagnostics.Debug.WriteLine("NullToVisibilityConverter");
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(object), typeof(Boolean))]
    public class NullToIsEnabledConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            System.Diagnostics.Debug.WriteLine("NullToIsEnabledConverter");
            return !(value == null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(XElement), typeof(String))]
    public class VehicleXmlToDescriptionString : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            XElement vehicle = value as XElement;
            return vehicle == null ? "<Error>" :
                String.Format("%s %s %s %s", vehicle.Attribute("year"), vehicle.Attribute("make"), vehicle.Attribute("model"), vehicle.Attribute("type"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region Helper Classes

    public class Vehicle {
        private String _year, _make, _model, _type;
        public Vehicle(String year, String make, String model, String engine) {
            _year = year;
            _make = make;
            _model = model;
            _type = engine;
        }
        public Vehicle(XElement vehicle) {
            _year = vehicle.Attribute("year").Value;
            _make = vehicle.Attribute("make").Value;
            _model = vehicle.Attribute("model").Value;
            _type = vehicle.Attribute("engine").Value;
        }

        public override string ToString() {
            return String.Format("{0} {1} {2} {3}", _year, _make, _model, _type);
        }

        public string ListDisplayText {
            get {
                return ToString();
            }
        }
        public string Make {
            get {
                return _make;
            }

            set {
                _make = value;
            }
        }

        public string Model {
            get {
                return _model;
            }

            set {
                _model = value;
            }
        }

        public string Type {
            get {
                return _type;
            }

            set {
                _type = value;
            }
        }

        public string Year {
            get {
                return _year;
            }

            set {
                _year = value;
            }
        }
    }

    #endregion
}
