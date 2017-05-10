using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Windows.Input;
using System.Net;
using Excel;
using System.Data;
using ArcGIS.Core.Geometry;
using Newtonsoft.Json;

using System.Collections.Specialized;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using Newtonsoft.Json.Linq;
using System.Windows.Media;

namespace Esri.APL.MilesPerDollar {
    internal class VehiclesPaneViewModel : DockPane {
        // CONSTS
        private const string STATE_ALLOW_FIND_SA = "mpd_allow_find_servicearea_state";
        private const double METERS_PER_MILE = 1609.34;
        // Colors need to be in order of descending MPG/polygon size
        private List<Color> _vehicleColors = new List<Color>() { Colors.LightGreen, Colors.Crimson };

        // Thread locking objects
        protected object _lockXmlYears = new object();

        #region Model variables and properties

        private XDocument _xmlAllVehicles;
        private ObservableCollection<Vehicle> _selectedVehicles;

        private ObservableCollection<string> _vehicleYears, _vehicleMakes, _vehicleModels, _vehicleTypes;
        // Since we're updating the dropdowns on the UI thread, no need to use the convoluted
        // read-only sync pattern shown in some of the samples.
        //private readonly ReadOnlyObservableCollection<string> _readonlyVehicleYears;

        private string _selectedVehicleYear, _selectedVehicleMake, _selectedVehicleModel, _selectedVehicleType;
        private XElement _selectedVehicle;

        private Dictionary<string, double> _paddZoneToFuelCost = new Dictionary<string, double>();
        public Dictionary<string, double> PADDZoneToFuelCost {
            get { return _paddZoneToFuelCost;  }
            set { _paddZoneToFuelCost = value; }
        }

        private Dictionary<string, string> _paddStateToZone = new Dictionary<string, string>();
        public Dictionary<string, string> PaddStateToZone {
            get {
                return _paddStateToZone;
            }

            set {
                _paddStateToZone = value;
            }
        }
        public ObservableCollection<string> VehicleYears {
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

        private void OnSelectedVehiclesChanged(object sender, NotifyCollectionChangedEventArgs e) {
            System.Diagnostics.Debug.WriteLine("OnSelectedVehiclesChanged");
            ObservableCollection<Vehicle> vehs = (ObservableCollection<Vehicle>)sender;
            if (vehs.Count <= 0) return;

            List<Vehicle> ovehs = vehs.OrderByDescending(vehicle => vehicle.Mpg).ToList();
            for (int iVeh = 0; iVeh < ovehs.Count(); iVeh++) {
                ovehs[iVeh].Color = _vehicleColors[iVeh].ToString();
            }
        }


        private void GetFuelPricePerPADDZone() {
            string sFuelPriceDataUrl = Properties.Settings.Default.FuelCostUrl;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(sFuelPriceDataUrl);
            req.ContentType = "application/ms-excel";
            Stream resp = req.GetResponse().GetResponseStream();
            MemoryStream ms = new MemoryStream(); resp.CopyTo(ms);
            IExcelDataReader xldr = ExcelReaderFactory.CreateBinaryReader(ms);
            DataTable priceSheet = xldr.AsDataSet().Tables[2];
            DataRow priceRow = priceSheet.Rows[priceSheet.Rows.Count - 2];
            PADDZoneToFuelCost.Add("I-A", Double.Parse(priceRow[2].ToString()));
            PADDZoneToFuelCost.Add("I-B", Double.Parse(priceRow[3].ToString()));
            PADDZoneToFuelCost.Add("I-C", Double.Parse(priceRow[4].ToString()));
            PADDZoneToFuelCost.Add("II", Double.Parse(priceRow[5].ToString()));
            PADDZoneToFuelCost.Add("III", Double.Parse(priceRow[6].ToString()));
            PADDZoneToFuelCost.Add("IV", Double.Parse(priceRow[7].ToString()));
            PADDZoneToFuelCost.Add("V", Double.Parse(priceRow[8].ToString()));
        }

        private void GetStatesPerPADDZone() {
            Uri uri = new Uri(Properties.Settings.Default.PADDZonesResourceUri);
            System.IO.Stream stIn = System.Windows.Application.GetResourceStream(uri).Stream;
            XDocument doc = XDocument.Load(stIn);
            //string sPaddZonesUrl = Properties.Settings.Default.PADDZonesUrl;
            //XDocument doc = XDocument.Load(sPaddZonesUrl);
            foreach (XElement elt in doc.Root.Elements(XName.Get("state"))) {
                PaddStateToZone.Add((string)elt.Attribute("name"), (string)elt.Attribute("padd"));
            }

        }
        private void GetVehicleData() {
            // Read vehicles data
            Uri uri = new Uri(Properties.Settings.Default.VehicleInfoResourceUri);
            System.IO.Stream stIn = System.Windows.Application.GetResourceStream(uri).Stream;
            _xmlAllVehicles = XDocument.Load(stIn);
        }
        private void GetVehicleYears() {
            var qryYears = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                           orderby vehicle.Attribute("year").Value
                           select vehicle.Attribute("year").Value;
            VehicleYears = new ObservableCollection<string>(qryYears.Distinct());
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
        VehiclesPaneViewModel() {
            // Set up necessary defaults
            _selectedVehicles = new ObservableCollection<Vehicle>();
            SelectedVehicles.CollectionChanged += OnSelectedVehiclesChanged;

            _addSelectedVehicleCommand = new RelayCommand(() => AddSelectedVehicle(), () => CanAddSelectedVehicle());
            _removeSelectedVehicleCommand = new RelayCommand((selected) => RemoveSelectedVehicle(selected), () => true);
            _startSAAnalysisCommand = new RelayCommand(() => StartSAAnalysis(), () => CanStartSAAnalysis());

            _driveDistPolys = new ObservableCollection<IDisposable>();
            //DriveDistPolys.CollectionChanged += OnDriveDistPolysChanged;
            _driveDistCircularBounds = new ObservableCollection<IDisposable>();
            //DriveDistCircularBounds.CollectionChanged += OnDriveDistCircularBoundsChanged;
        }
        ~VehiclesPaneViewModel() {
            FrameworkApplication.SetCurrentToolAsync(_previousActiveTool);
            // Clear out any onscreen graphics
            foreach (IDisposable graphic in DriveDistPolys) {
                graphic.Dispose();
            }
            foreach (IDisposable graphic in DriveDistCircularBounds) {
                graphic.Dispose();
            }
        }

        protected override Task InitializeAsync() {
            // Populate data lists
            GetVehicleData();
            GetFuelPricePerPADDZone();
            GetStatesPerPADDZone();
            // Prepopulate years dropdown
            GetVehicleYears();
            return base.InitializeAsync();
        }
        #endregion

        #region Add/Remove Vehicle command

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
        private void RemoveSelectedVehicle(object selected) {
            System.Diagnostics.Debug.WriteLine("RemoveSelectedVehicle: " + (selected as Vehicle)?.ToString());
            if (selected is Vehicle) SelectedVehicles.Remove((Vehicle)selected);
        }
        #endregion

        #region Start Analysis command / Perform Analysis

        private ICommand _startSAAnalysisCommand;
        public ICommand StartSAAnalysisCommand => _startSAAnalysisCommand;

        private ObservableCollection<IDisposable> _driveDistPolys;
        public ObservableCollection<IDisposable> DriveDistPolys {
            get { return _driveDistPolys;  }
            set { _driveDistPolys = value; }
        }
        private ObservableCollection<IDisposable> _driveDistCircularBounds;
        public ObservableCollection<IDisposable> DriveDistCircularBounds {
            get { return _driveDistCircularBounds; }
            set { _driveDistCircularBounds = value; }
        }
        //private void OnDriveDistPolysChanged(object sender, NotifyCollectionChangedEventArgs e) {
        //    switch (e.Action) {
        //        case NotifyCollectionChangedAction.Remove:
        //        case NotifyCollectionChangedAction.Replace:
        //        case NotifyCollectionChangedAction.Reset:
        //            if (e.OldItems != null)
        //                foreach (IDisposable graphic in e.OldItems) graphic.Dispose();
        //            break;
        //     }
        //}
        //private void OnDriveDistCircularBoundsChanged(object sender, NotifyCollectionChangedEventArgs e) {
        //    switch (e.Action) {
        //        case NotifyCollectionChangedAction.Remove:
        //        case NotifyCollectionChangedAction.Replace:
        //        case NotifyCollectionChangedAction.Reset:
        //            if (e.OldItems != null)
        //                foreach (IDisposable graphic in e.OldItems) graphic.Dispose();
        //            break;
        //     }
        //}
        public bool CanStartSAAnalysis() {
            bool enoughVehiclesSelected = SelectedVehicles.Count > 0;
            bool mapPaneActive = FrameworkApplication.State.Contains(DAML.State.esri_mapping_mapPane);
            bool oktoStartSAAnalysis = enoughVehiclesSelected && mapPaneActive;

            if (oktoStartSAAnalysis) FrameworkApplication.State.Activate(STATE_ALLOW_FIND_SA);
            else FrameworkApplication.State.Deactivate(STATE_ALLOW_FIND_SA);

            return oktoStartSAAnalysis;
        }

        //TODO Blog about programmatic invocation of invisible MapTool
        string _previousActiveTool = null;
        private void StartSAAnalysis() {
            _previousActiveTool = FrameworkApplication.CurrentTool;
            FrameworkApplication.SetCurrentToolAsync(MPDSATool.TOOL_ID);
        }


        internal async Task PerformAnalysis(MapPoint ptStartLoc, MapView mapView, ProgressorSource ps) {
            ps.Message = "Gathering and verifying parameter data...";
            string sReqUrl = Properties.Settings.Default.QryPointToState;
            string sReq = String.Format("{0}?returnGeometry=false&returnDistinctValues=false&geometry={1}&geometryType=esriGeometryPoint&f=json&outFields=*&spatialRel=esriSpatialRelIntersects",
                            sReqUrl, ptStartLoc.ToJson());
            // Find out what state the user clicked; or report an error if outside the U.S.
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(sReq);
            string sResp;
            using (StreamReader sread = new StreamReader(req.GetResponse().GetResponseStream()))
                sResp = sread.ReadToEnd();            
            dynamic respPADDState = JsonConvert.DeserializeObject(sResp);

            if (respPADDState.features.Count < 1) {
                MessageBox.Show("Please choose a spot within the U.S.A.");
                return /*null*/;
            }

            string sState = respPADDState.features[0].attributes.STATE.ToString();

            // Find out what PADD zone the state is in
            string sPADDZone = PaddStateToZone[sState];

            // Find out the gallons/$1.00 in that PADD zone
            double nFuelCost = PADDZoneToFuelCost[sPADDZone];

            // Find out the miles per dollar each vehicle: (mi / gal) / (dollars / gal)            
            // Map is in meters, so convert miles to meters
            IEnumerable<double> vehicleMetersPerDollar =
                SelectedVehicles.Select(vehicle => (vehicle.Mpg * METERS_PER_MILE) / nFuelCost);

            string sDistsParam = String.Join(" ", vehicleMetersPerDollar.ToArray());
            MapPoint ptStartLocNoZ = await QueuedTask.Run(() => {
                MapPoint ptNoZ = MapPointBuilder.CreateMapPoint(ptStartLoc.X, ptStartLoc.Y, ptStartLoc.SpatialReference);
                return ptNoZ;
            });

            // ARGH! No corresponding type for the needed GPFeatureRecordSetLayer parameter!
            //TODO blog about this...stuff
            string sStartGeom = ptStartLocNoZ.ToJson();
            string sStartLocParam = "{\"geometryType\":\"esriGeometryPoint\",\"features\":[{\"geometry\":" + sStartGeom + "}]}";


            // Run the query
            ps.Message = "Running the drive distance analysis...";
            string sGPUrl = Properties.Settings.Default.GPFindSA;
            sGPUrl += String.Format("?Distances={0}&Start_Location={1}&f=json", sDistsParam, sStartLocParam);
            HttpWebRequest reqSA = (HttpWebRequest)WebRequest.Create(sGPUrl);
            HttpWebResponse wr;
            try {
                wr = (HttpWebResponse)reqSA.GetResponse();
                if (wr.StatusCode != HttpStatusCode.OK) {
                    MessageBox.Show("Error running analysis: " + wr.StatusDescription);
                    return;
                }
            } catch (WebException e) {
                MessageBox.Show("Error running analysis: " + e.Message);
                return;
            }

            using (StreamReader sread = new StreamReader(wr.GetResponseStream()))
            sResp = sread.ReadToEnd();
            
            JObject respAnalysis = JObject.Parse(sResp);

            JArray feats = respAnalysis["results"][0]["value"]["features"] as JArray;

            // Order descending so that largest polygon is added to the map first
            List<JToken> aryResFeats = feats.OrderByDescending(feat => feat["attributes"]["Shape_Area"].ToObject<Double>()).ToList();

            int iSR = respAnalysis["results"][0]["value"]["spatialReference"]["wkid"].ToObject<Int32>();
            SpatialReference sr = await QueuedTask.Run<SpatialReference>(() => {
                SpatialReference srTemp = SpatialReferenceBuilder.CreateSpatialReference(iSR);
                return srTemp;
            });

            //TODO Support variable numbers of results
            // Currently we assume 1 or 2 for simplicity assigning colors. More may require setting up a color ramp or scheme.
            
            // Dispose all graphics before calling DriveDistPolys.Clear(); 
            foreach (IDisposable graphic in DriveDistPolys) {
                graphic.Dispose();
            }
            DriveDistPolys.Clear();
            foreach (IDisposable graphic in DriveDistCircularBounds) {
                graphic.Dispose();
            }
            DriveDistCircularBounds.Clear();

            for (int iRes = 0; iRes < aryResFeats.Count(); iRes++) {
                string sGeom = aryResFeats[iRes]["geometry"].ToString();
                Polygon poly = await QueuedTask.Run(() => {
                    Polygon polyNoSR = PolygonBuilder.FromJson(sGeom);
                    return PolygonBuilder.CreatePolygon(polyNoSR, sr);
                });
                CIMStroke outline = SymbolFactory.ConstructStroke(ColorFactory.BlackRGB, 1.0, SimpleLineStyle.Solid);
                CIMPolygonSymbol symPoly = SymbolFactory.ConstructPolygonSymbol(
                    ColorFactory.CreateRGBColor(_vehicleColors[iRes].R, _vehicleColors[iRes].G, _vehicleColors[iRes].B, 70), 
                    SimpleFillStyle.Solid, outline);
                CIMSymbolReference sym = symPoly.MakeSymbolReference();
                CIMSymbolReference symDef = SymbolFactory.DefaultPolygonSymbol.MakeSymbolReference();
                IDisposable graphic = await QueuedTask.Run(() => {
                    return mapView.AddOverlay(poly, sym);
                });
                DriveDistPolys.Add(graphic);
            }
            //return resp;
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
        internal static VehiclesPaneViewModel _instance = null;
        /// <summary>
        /// Get the single instance of the ViewModel. This is a way to pass data, or execute code from, other code-behinds.
        /// </summary>
        internal static VehiclesPaneViewModel instance {
            get {
                if (_instance == null) {
                    _instance = (VehiclesPaneViewModel)FrameworkApplication.DockPaneManager.Find(_dockPaneID);
                }
                return _instance;
            }
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

    [ValueConversion(typeof(XElement), typeof(string))]
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
    //TODO blog about PropertyChangedBase for color binding
    public class Vehicle : PropertyChangedBase {
        private string _year, _make, _model, _type;
        private int _mpg;
        private string _color;
        public Vehicle(string year, string make, string model, string engine, int mpg) {
            _year = year;
            _make = make;
            _model = model;
            _type = engine;
            _mpg = mpg;
        }
        public Vehicle(XElement vehicle) {
            _year = vehicle.Attribute("year").Value;
            _make = vehicle.Attribute("make").Value;
            _model = vehicle.Attribute("model").Value;
            _type = vehicle.Attribute("engine").Value;
            _mpg = Int32.Parse(vehicle.Attribute("mpg").Value);
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

        /// <summary>
        /// The color used to display the drive-distance polygon and list item for this vehicle.
        /// </summary>
        public string Color {
            get {
                return _color;
            }

            set {
                SetProperty(ref _color, value);
            }
        }

        public int Mpg {
            get {
                return _mpg;
            }

            set {
                _mpg = value;
            }
        }
    }

    #endregion
}
