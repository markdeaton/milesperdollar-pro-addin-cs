using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Net;
using System.IO;
using Newtonsoft.Json;

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

            VehiclesPaneViewModel.instance.PerformAnalysis(geometry as MapPoint);

            return base.OnSketchCompleteAsync(geometry);
        }
    }
}
