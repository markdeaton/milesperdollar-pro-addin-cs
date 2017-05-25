using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Threading.Tasks;

namespace Esri.APL.MilesPerDollar {
    internal class MPDSATool : MapTool {
        public static String TOOL_ID = "Esri_APL_MilesPerDollar_MPDSATool";

        public MPDSATool() {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Point;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task OnToolActivateAsync(bool active) {
            System.Diagnostics.Debug.WriteLine("Map tool activated: " + active.ToString());
            return base.OnToolActivateAsync(active);
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry) {
            System.Diagnostics.Debug.WriteLine("Map tool sketch complete");

            //TODO blog about creating ViewModel static instance var so we can call it from codebehind
            MapView mapView = MapView.Active;

            ProgressorSource ps = new ProgressorSource("Running the drive distance analysis...");
            QueuedTask.Run(() => VehiclesPaneViewModel.Instance.PerformAnalysis(geometry as MapPoint, mapView, ps), ps.Progressor);

            return base.OnSketchCompleteAsync(geometry);
        }
    }
}
