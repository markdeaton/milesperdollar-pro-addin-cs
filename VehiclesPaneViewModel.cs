using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Esri.APL.MilesPerDollar {
    internal class VehiclesPaneViewModel : DockPane {
        // CONSTS
        private const string STATE_ALLOW_FIND_SA = "mpd_allow_find_servicearea_state";
        private const byte RESULT_OPACITY_PCT = 20;
        private const int MAX_VEHICLES = 5;

        #region Model variables and properties

        private XDocument _xmlAllVehicles;
        private ObservableCollection<Vehicle> _selectedVehicles;

        private ObservableCollection<string> _vehicleYears, _vehicleMakes, _vehicleModels, _vehicleTypes;

        // Since we're updating the dropdowns on the UI thread, no need to use the 
        // synchronization pattern the way we do with the Results collection.
        //private readonly ReadOnlyObservableCollection<string> _readonlyVehicleYears;

        private string _selectedVehicleYear, _selectedVehicleMake, _selectedVehicleModel, _selectedVehicleType;
        private XElement _selectedVehicle;
        private string _selectedPADDZone;

        private Dictionary<string, double> _paddZoneToFuelCost = new Dictionary<string, double>();
        public Dictionary<string, double> PADDZoneToFuelCost {
            get { return _paddZoneToFuelCost; }
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
            set { SetProperty(ref _vehicleModels, value); }
        }

        public ObservableCollection<string> VehicleTypes {
            get { return _vehicleTypes; }
            set { SetProperty(ref _vehicleTypes, value); }
        }

        public string SelectedVehicleYear {
            get { return _selectedVehicleYear; }
            set {
                SetProperty(ref _selectedVehicleYear, value);
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

        private object _lockResults = new object(); // locking object
        private ObservableCollection<Result> _results = new ObservableCollection<Result>();
        public ObservableCollection<Result> Results {
            get { return _results; }
            set { SetProperty(ref _results, value); }
        }

        public string SelectedPADDZone {
            get {
                return _selectedPADDZone;
            }
            set {
                SetProperty(ref _selectedPADDZone, value);
            }
        }
        //private void OnSelectedVehiclesChanged(object sender, NotifyCollectionChangedEventArgs e) {
        //    System.Diagnostics.Debug.WriteLine("OnSelectedVehiclesChanged");
        //    ObservableCollection<Vehicle> vehs = (ObservableCollection<Vehicle>)sender;
        //    if (vehs.Count <= 0) return;

        //    List<Vehicle> ovehs = vehs.OrderBy(vehicle => vehicle.Mpg).ToList();
        //    for (int iVeh = 0; iVeh < ovehs.Count; iVeh++) {
        //        ovehs[iVeh].Color = _vehicleColors[iVeh].ToString();
        //    }
        //}

        private void GetFuelPricePerPADDZone() {
            try {
                string sFuelPriceDataUrl = Properties.Settings.Default.FuelCostUrl;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(sFuelPriceDataUrl);
                req.ContentType = "application/ms-excel";
                Stream resp = null;
                resp = req.GetResponse().GetResponseStream();
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
            } catch (Exception e) {
                throw new Exception("Error while getting latest fuel price data.", e);
            }
        }

        private void GetStatesPerPADDZone() {
            try {
                Uri uri = new Uri(Properties.Settings.Default.PADDZonesResourceUri);
                System.IO.Stream stIn = System.Windows.Application.GetResourceStream(uri).Stream;
                XDocument doc = XDocument.Load(stIn);
                foreach (XElement elt in doc.Root.Elements(XName.Get("state"))) {
                    PaddStateToZone.Add((string)elt.Attribute("name"), (string)elt.Attribute("padd"));
                }
            } catch (Exception e) {
                throw new Exception("Error getting states per PADD zone.", e);
            }
        }
        private void GetVehicleData() {
            // Read vehicles data
            try {
                Uri uri = new Uri(Properties.Settings.Default.VehicleInfoResourceUri);
                System.IO.Stream stIn = System.Windows.Application.GetResourceStream(uri).Stream;
                _xmlAllVehicles = XDocument.Load(stIn);
            } catch (Exception e) {
                throw new Exception("Error getting and reading vehicle data.", e);
            }
        }
        private void GetVehicleYears() {
            var qryYears = from vehicle in _xmlAllVehicles.Root.Elements("vehicle")
                           orderby vehicle.Attribute("year").Value descending
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
            //SelectedVehicles.CollectionChanged += OnSelectedVehiclesChanged;

            _addSelectedVehicleCommand = new RelayCommand(() => AddSelectedVehicle(), () => CanAddSelectedVehicle());
            _removeSelectedVehicleCommand = new RelayCommand((selected) => RemoveSelectedVehicle(selected), () => true);
            _startSAAnalysisCommand = new RelayCommand(() => StartSAAnalysis(), () => CanStartSAAnalysis());
            _saveResultsCommand = new RelayCommand(() => SaveResults(), () => CanSaveResults());
            _resetAnalysisCommand = new RelayCommand(() => ResetAnalysis());

            _results = new ObservableCollection<Result>();
            BindingOperations.EnableCollectionSynchronization(_results, _lockResults);
        }
        protected override Task UninitializeAsync() {
            try {
                FrameworkApplication.SetCurrentToolAsync(_previousActiveTool);
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine("Error setting previous tool: " + e.Message);
            }
            // Clear out any onscreen graphics
            try {
                Results.ClearResults();
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine("Error clearing result polygons: " + e.Message);
            }
            return base.UninitializeAsync();
        }

        protected override Task InitializeAsync() {
            // Populate data lists
            try {
                GetVehicleData();
                GetFuelPricePerPADDZone();
                GetStatesPerPADDZone();
                // Prepopulate years dropdown
                GetVehicleYears();
            } catch (Exception e) {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Error during initialization: " + e.Message);
            }
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
            bool maxVehiclesAlreadyChosen = SelectedVehicles.Count >= MAX_VEHICLES;
            return vehicleSelected && !maxVehiclesAlreadyChosen;
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

        #region Reset Analysis command
        private ICommand _resetAnalysisCommand;
        public ICommand ResetAnalysisCommand => _resetAnalysisCommand;
        public void ResetAnalysis() {
            SelectedVehicles.Clear();
            Results.ClearResults();
            System.Diagnostics.Debug.WriteLine("Reset Analysis");
        }

        #endregion

        #region Save Results command
        public ICommand SaveResultsCommand => _saveResultsCommand;
        private ICommand _saveResultsCommand;

        public bool CanSaveResults() {
            return Results.Count > 0;
        }
        public void SaveResults() {
            // Check for a feature layer connected to a feature class with the right name, type, etc.
            QueuedTask.Run(async () => {
            ProgressDialog pd;
            Geodatabase fgdb = null;
            try {
                string resultFcName = Properties.Settings.Default.ResultFeatureClassName;
                List<FeatureLayer> featureLayers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                FeatureLayer flResults = featureLayers.Find(lyr => lyr.GetFeatureClass().GetName() == resultFcName);
                FeatureClass fc = null;

                if (flResults == null) {
                    // Look for a FC to connect it to
                    string defFgdbPath = Project.Current.DefaultGeodatabasePath;
                    try {
                        fgdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(defFgdbPath)));
                        IReadOnlyList<string> gpParams;
                        try {
                            fc = fgdb.OpenDataset<FeatureClass>(resultFcName);
                        } catch (GeodatabaseException) {
                            // Create results feature class
                            pd = new ProgressDialog("Creating feature class..."); pd.Show();
                            string sTemplatePath = Path.Combine(
                                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                                @"Resources\Template.gdb\MilesPerDollar_template");
                            gpParams = Geoprocessing.MakeValueArray(
                                defFgdbPath, resultFcName, "POLYGON", sTemplatePath, null, null,
                                SpatialReferenceBuilder.CreateSpatialReference(Properties.Settings.Default.ResultFeatureClassSRWkid));
                            IGPResult resCreateFC = await Geoprocessing.ExecuteToolAsync("management.CreateFeatureclass", gpParams, flags: GPExecuteToolFlags.None);
                            if (resCreateFC.IsFailed) {
                                pd.Hide();
                                List<string> errMsgs = resCreateFC.ErrorMessages.Select(errMsg => errMsg.Text).ToList();
                                string sErrMsgs = String.Join("\n", errMsgs);
                                throw new Exception("Error creating results feature class:" + sErrMsgs);
                            }
                            pd.Hide();
                            fc = fgdb.OpenDataset<FeatureClass>(resultFcName);
                        }
                        } catch (Exception e) {
                            throw new Exception("Error opening or creating feature class", e);
                        }
                        flResults = LayerFactory.Instance.CreateFeatureLayer(fc, MapView.Active.Map, 0, "Miles Per Dollar Analysis Results");
                    } else {
                        fc = flResults.GetFeatureClass();
                    }

                    flResults?.SetVisibility(false);

                    //pd = new ProgressDialog("Creating result schema..."); pd.Show();
                    //try {
                    //    await AddResultFcFields(fc);
                    //} catch (Exception e) {
                    //    throw new Exception("Error adding fields to result feature class", e);
                    //} finally { pd.Hide();  }

                    // By default, the GDB is added to the map via a new feature layer
                    pd = new ProgressDialog("Creating result features..."); pd.Show();
                    try {
                        await AddResultFeatures(fc);
                    } catch (Exception e) {
                        throw new Exception("Error creating result features", e);
                    } finally { pd.Hide(); }

                    // If we got here, it was sucessful; we can discard the graphic overlays
                    Results.ClearResults();

                    flResults?.SetVisibility(true);
                } catch (Exception e) {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                    string sMsg = e.Message + ":\n" + e.InnerException.Message;
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(sMsg);
                } finally {
                    fgdb?.Dispose();
                }
            });
        }

        private async Task AddResultFeatures(FeatureClass fc) {
            EditOperation featOp = new EditOperation();
            featOp.Callback(context => {
                foreach (Result result in Results) {
                    using (RowBuffer row = fc.CreateRowBuffer()) {
                        row["VehicleYear"] = result.Vehicle.Year;
                        row["VehicleMake"] = result.Vehicle.Make;
                        row["VehicleModel"] = result.Vehicle.Model;
                        row["Vehicletype"] = result.Vehicle.Type;
                        row["VehicleMPG"] = result.Vehicle.Mpg;
                        row["OriginalSymbolColor"] = result.Color;
                        row["PADDZone"] = result.PaddZone;
                        row["DOEGasPricePerGallon"] = result.DollarsPerGallon;
                        row["MilesPerDollar"] = result.MilesPerDollar;
                        row["DriveDistanceMiles"] = result.DriveDistMi;
                        row["ResultDateTime"] = result.ResultDateTimeUTC;
                        row[fc.GetDefinition().GetShapeField()] = result.DriveServiceArea;

                        using (Feature feat = fc.CreateRow(row)) {
                            context.Invalidate(feat);
                        }
                    }
                }
            }, fc);
            bool success = await featOp.ExecuteAsync();
            if (!success) throw new Exception("Error adding result features: " + featOp.ErrorMessage);
            success = await Project.Current.SaveEditsAsync();
            if (!success) throw new Exception("Failure while saving result features");
        }

        //private async Task AddResultFcFields(FeatureClass fc) {
        //    // Add Fields
        //    IGPResult resCreateField;
        //    IReadOnlyList<string> gpParams;
        //    string fieldName;

        //    // Vehicle year, make, model, type, MPG
        //    fieldName = "VehicleYear";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 4);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "VehicleMake";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 100);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "VehicleModel";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 100);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "VehicleType";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 100);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "VehicleMPG";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "SHORT");
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "OriginalSymbolColor";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 9);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    // Result PADD zone, dollars per gallon, miles per dollar, drive distance (miles)
        //    fieldName = "PADDZone";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 5);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "DOEGasPricePerGallon";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "FLOAT");
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "MilesPerDollar";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "FLOAT", null, null, 100);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    fieldName = "DriveDistanceMiles";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "FLOAT");
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    // Result date/time
        //    fieldName = "ResultDateTime";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "DATE");
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);

        //    // Comments
        //    fieldName = "Comments";
        //    gpParams = Geoprocessing.MakeValueArray(fc, fieldName, "TEXT", null, null, 255);
        //    resCreateField = await Geoprocessing.ExecuteToolAsync("management.AddField", gpParams, flags: GPExecuteToolFlags.None);
        //    CheckAddFieldGpSuccess(resCreateField);
        //}

        //private void CheckAddFieldGpSuccess(IGPResult gpResult) {
        //    string sField = gpResult.Parameters.ToArray()[1].Item3;
        //    string sMsg;

        //    if (gpResult.IsFailed) {
        //        List<string> errMsgs = gpResult.ErrorMessages.Select(errMsg => errMsg.Text).ToList();
        //        string sErrMsgs = String.Join("\n", errMsgs);
        //        sMsg = "Error adding field " + sField + ": " + errMsgs;
        //        System.Diagnostics.Debug.WriteLine(sMsg);
        //        throw new Exception(sMsg);
        //    } else if (gpResult.IsCanceled) {
        //        sMsg = "Canceled addding field " + sField;
        //        System.Diagnostics.Debug.WriteLine(sMsg);
        //        throw new Exception(sMsg);
        //    } else {
        //        sMsg = "Successfully added field " + sField;
        //        System.Diagnostics.Debug.WriteLine(sMsg);
        //    }
        //}
        #endregion

        #region Start Analysis command / Perform Analysis

        private ICommand _startSAAnalysisCommand;
        public ICommand StartSAAnalysisCommand => _startSAAnalysisCommand;
        public bool CanStartSAAnalysis() {
            bool enoughVehiclesSelected = SelectedVehicles.Count > 0;
            bool mapPaneActive = FrameworkApplication.State.Contains(DAML.State.esri_mapping_mapPane);
            bool oktoStartSAAnalysis = enoughVehiclesSelected && mapPaneActive;

            if (oktoStartSAAnalysis) FrameworkApplication.State.Activate(STATE_ALLOW_FIND_SA);
            else FrameworkApplication.State.Deactivate(STATE_ALLOW_FIND_SA);

            return oktoStartSAAnalysis;
        }

        string _previousActiveTool = null;
        private void StartSAAnalysis() {
            _previousActiveTool = FrameworkApplication.CurrentTool;
            FrameworkApplication.SetCurrentToolAsync(MPDSATool.TOOL_ID);
        }


        internal async Task PerformAnalysis(MapPoint ptStartLoc, MapView mapView, ProgressorSource ps) {
            ps.Progressor.Message = "Running the analysis...";
            ps.Progressor.Status = "Gathering and verifying parameter data";
            string sReqUrl = Properties.Settings.Default.QryPointToState;
            string sReq = String.Format("{0}?returnGeometry=false&returnDistinctValues=false&geometry={1}&geometryType=esriGeometryPoint&f=json&outFields=*&spatialRel=esriSpatialRelIntersects",
                            sReqUrl, ptStartLoc.ToJson());
            // Find out what state the user clicked; or report an error if outside the U.S.
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(sReq);
            string sResp;
            try {
                using (StreamReader sread = new StreamReader(req.GetResponse().GetResponseStream()))
                    sResp = sread.ReadToEnd();
            } catch (Exception e) {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Error mapping the chosen spot to a petroleum area district in the U.S.A.: " + e.Message);
                return;
            }
            dynamic respPADDState = JsonConvert.DeserializeObject(sResp);

            try {
                string sState = respPADDState.features[0].attributes.STATE.ToString();
                // Find out what PADD zone the state is in
                SelectedPADDZone = PaddStateToZone[sState];
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine("Exception getting PADD for chosen spot: " + e.Message);
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Please choose a spot within the U.S.A.");
                return /*null*/;
            }

            // Find out the gallons/$1.00 in that PADD zone
            double nFuelCost = PADDZoneToFuelCost[SelectedPADDZone];

            // Find out the miles per dollar each vehicle: (mi / gal) / (dollars / gal)            
            // Map is in meters, so convert miles to meters
            Vehicle[] orderedVehicles = SelectedVehicles.OrderBy(vehicle => vehicle.Mpg).ToArray<Vehicle>();
            IEnumerable<double> vehicleMetersPerDollar =
                orderedVehicles.Select(vehicle => (vehicle.MetersPerGallon) / nFuelCost);

            string sDistsParam = String.Join(" ", vehicleMetersPerDollar.ToArray());
            MapPoint ptStartLocNoZ = await QueuedTask.Run(() => {
                MapPoint ptNoZ = MapPointBuilder.CreateMapPoint(ptStartLoc.X, ptStartLoc.Y, ptStartLoc.SpatialReference);
                return ptNoZ;
            });

            // No corresponding type for the needed GPFeatureRecordSetLayer parameter
            string sStartGeom = ptStartLocNoZ.ToJson();
            string sStartLocParam = "{\"geometryType\":\"esriGeometryPoint\",\"features\":[{\"geometry\":" + sStartGeom + "}]}";


            // Run the query
            ps.Progressor.Status = "Executing the analysis";
            string sGPUrl = Properties.Settings.Default.GPFindSA;
            sGPUrl += String.Format("?Distances={0}&Start_Location={1}&f=json", sDistsParam, sStartLocParam);
            HttpWebRequest reqSA = (HttpWebRequest)WebRequest.Create(sGPUrl);
            HttpWebResponse wr;
            try {
                wr = (HttpWebResponse)reqSA.GetResponse();
                if (wr.StatusCode != HttpStatusCode.OK) {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Error running analysis: " + wr.StatusDescription);
                    return;
                }
            } catch (WebException e) {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Error running analysis: " + e.Message);
                return;
            }

            // Show the results
            ps.Progressor.Status = "Processing the results";

            using (StreamReader sread = new StreamReader(wr.GetResponseStream()))
                sResp = sread.ReadToEnd();

            JObject respAnalysis = JObject.Parse(sResp);

            JArray feats = respAnalysis["results"][0]["value"]["features"] as JArray;

            // Rectify results and order so that largest polygon can be added to the map first
            List<JToken> aryResFeats = RectifyResults(feats, orderedVehicles);

            int iSR = respAnalysis["results"][0]["value"]["spatialReference"]["wkid"].ToObject<Int32>();
            SpatialReference sr = await QueuedTask.Run<SpatialReference>(() => {
                SpatialReference srTemp = SpatialReferenceBuilder.CreateSpatialReference(iSR);
                return srTemp;
            });

            /*lock (_lockResults)*/
            Results.ClearResults();

            // Iterate backwards to add larger polygons behind smaller ones

            for (int iVeh = orderedVehicles.Count() - 1; iVeh >= 0; iVeh--) {
                Result result = new Result(orderedVehicles[iVeh]);
                Polygon poly = null;
                IDisposable graphic = null;

                // Compute color for this result
                float multiplier = aryResFeats.Count > 1 ? ((float)iVeh) / ((float)(aryResFeats.Count - 1)) : 0;
                byte red = (byte)(255 - (255 * multiplier));
                byte green = (byte)(255 * multiplier);
                Color color = Color.FromRgb(red, green, 0);
                result.Color = color.ToString();

                result.PaddZone = SelectedPADDZone;
                result.DollarsPerGallon = nFuelCost;

                string sGeom = aryResFeats[iVeh]["geometry"].ToString();
                poly = await QueuedTask.Run(() => {
                    Polygon polyNoSR = PolygonBuilder.FromJson(sGeom);
                    return PolygonBuilder.CreatePolygon(polyNoSR, sr);
                });
                CIMStroke outline = SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.BlackRGB, 1.0, SimpleLineStyle.Solid);
                CIMPolygonSymbol symPoly = SymbolFactory.Instance.ConstructPolygonSymbol(
                    ColorFactory.Instance.CreateRGBColor(color.R, color.G, color.B, RESULT_OPACITY_PCT),
                    SimpleFillStyle.Solid, outline);
                CIMSymbolReference sym = symPoly.MakeSymbolReference();
                CIMSymbolReference symDef = SymbolFactory.Instance.DefaultPolygonSymbol.MakeSymbolReference();
                graphic = await QueuedTask.Run(() => {
                    return mapView.AddOverlay(poly, sym);
                });

                result.DriveServiceArea = poly;
                result.DriveServiceAreaGraphic = graphic;
                result.DriveDistM = aryResFeats[iVeh]["attributes"]["ToBreak"].Value<double>();
                /*lock (_lockResults)*/
                Results.Add(result);
            }
        }

        /// <summary>
        /// If multiple vehicles have the same MPG, we don't get multiple results for them;
        //  results for those vehicles get collapsed. This will fill out the results, duplicating if needed.
        /// </summary>
        /// <param name="feats"></param>
        /// <param name="orderedVehicles"></param>
        /// <returns>List<JToken> or ordered and rectified results</returns>
        private List<JToken> RectifyResults(JArray feats, Vehicle[] orderedVehicles) {
            List<JToken> aryResFeats = feats.OrderBy(feat => feat["attributes"]["Shape_Area"].ToObject<Double>()).ToList();
            int howManyMoreVehiclesThanResults = orderedVehicles.Count() - aryResFeats.Count;
            int resultsDuplicatedSoFar = 0;

            for (int iVeh = orderedVehicles.Count() - 1; iVeh >= 0; iVeh--) {
                bool duplicateThePreviousResultForThisVehicle =
                    howManyMoreVehiclesThanResults > 0
                    && iVeh < orderedVehicles.Count() - 1
                    && orderedVehicles[iVeh].Mpg == orderedVehicles[iVeh + 1].Mpg;

                if (duplicateThePreviousResultForThisVehicle) {
                    int iCurrentResultPosition = iVeh - howManyMoreVehiclesThanResults + resultsDuplicatedSoFar + 1;
                    JToken resultToDuplicate = aryResFeats[iCurrentResultPosition];
                    JToken duplicatedResult = resultToDuplicate.DeepClone();
                    aryResFeats.Insert(iCurrentResultPosition, duplicatedResult);
                    resultsDuplicatedSoFar++;
                }
            }
            return aryResFeats;
        }
        #endregion

        #region Dockpane Plumbing

        private const string DOCKPANE_ID = "Esri_APL_MilesPerDollar_VehiclesPane";

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show() {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(DOCKPANE_ID);
            if (pane == null)
                return;

            pane.Activate();
        }
        internal static VehiclesPaneViewModel _instance = null;
        /// <summary>
        /// Get the single instance of the ViewModel. This is a way to pass data, or execute code from, other code-behinds.
        /// </summary>
        internal static VehiclesPaneViewModel Instance {
            get {
                if (_instance == null) {
                    _instance = (VehiclesPaneViewModel)FrameworkApplication.DockPaneManager.Find(DOCKPANE_ID);
                }
                return _instance;
            }
        }
        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Miles per Dollar";

        public string Heading {
            get { return _heading; }
            set {
                SetProperty(ref _heading, value, () => Heading);
            }
        }
        #endregion
    }

    #region Extension Methods
    /// <summary>
    /// Dispose of all polygon drive and circular bound graphics before clearing results
    /// </summary>
    internal static class ResultsExtensions {
        internal static void ClearResults(this ObservableCollection<Result> results) {
            foreach (Result result in results) {
                result.DriveServiceAreaGraphic?.Dispose();
                result.DriveCircularBoundGraphic?.Dispose();
            }
            results.Clear();
        }
    }
    #endregion

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class VehiclesPane_ShowButton : Button {
        protected override void OnClick() {
            VehiclesPaneViewModel.Show();
        }
    }

}
