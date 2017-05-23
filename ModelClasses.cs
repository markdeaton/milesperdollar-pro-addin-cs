#region Helper Classes
//TODO blog about PropertyChangedBase for color binding
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using System;
using System.Xml.Linq;

namespace Esri.APL.MilesPerDollar {
    public class Vehicle : PropertyChangedBase {
        private string _year, _make, _model, _type;
        private int _mpg;
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

        internal static readonly double METERS_PER_MILE = 1609.34;

        public override string ToString() {
            return LongDescription;
        }
        public string ShortDescription {
            get {
                return String.Format("{0} {1} {2}", Year, Make, Model);
            }
        }

        public string LongDescription {
            get {
                return String.Format("{0} {1} {2} {3}", Year, Make, Model, Type);
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

        public int Mpg {
            get {
                return _mpg;
            }

            set {
                _mpg = value;
            }
        }

        public double MetersPerGallon => Mpg * METERS_PER_MILE;
    }
    public class Result : PropertyChangedBase {
        private Vehicle _vehicle;
        private Polygon _driveServiceArea;
        private Polygon _driveCircularBound;
        private double _driveDistM;
        private string _paddZone;
        private double _dollarsPerGallon;
        private string _color;
        private DateTime _resultDateTimeUTC = DateTime.UtcNow;
        private IDisposable _driveServiceAreaGraphic, _driveCircularBoundGraphic;

        public Result(Vehicle vehicle) {
            this.Vehicle = vehicle;
        }
        public Result(Vehicle vehicle, DateTime resultDT) {
            this.Vehicle = vehicle;
            this._resultDateTimeUTC = resultDT;
        }

        public Vehicle Vehicle {
            get {
                return _vehicle;
            }
            set {
                SetProperty(ref _vehicle, value);
            }
        }

        public Polygon DriveServiceArea {
            get {
                return _driveServiceArea;
            }
            set {
                SetProperty(ref _driveServiceArea, value);
            }
        }

        public Polygon DriveCircularBound {
            get {
                return _driveCircularBound;
            }
            set {
                SetProperty(ref _driveCircularBound, value);
            }
        }

        public IDisposable DriveServiceAreaGraphic {
            get {
                return _driveServiceAreaGraphic;
            }

            set {
                _driveServiceAreaGraphic = value;
            }
        }

        public IDisposable DriveCircularBoundGraphic {
            get {
                return _driveCircularBoundGraphic;
            }

            set {
                _driveCircularBoundGraphic = value;
            }
        }

        public double DriveDistM {
            get {
                return _driveDistM;
            }

            set {
                SetProperty(ref _driveDistM, value);
            }
        }
        /// <summary>
        /// The original color displayed for this result
        /// </summary>
        public string Color {
            get {
                return _color;
            }

            set {
                SetProperty(ref _color, value);
            }
        }

        public double DriveDistMi => Math.Round(DriveDistM / Vehicle.METERS_PER_MILE, 1);

        /// <summary>
        /// Date and time this result was generated, expressed in the UTC time zone
        /// </summary>
        public DateTime ResultDateTimeUTC => _resultDateTimeUTC;

        public string PaddZone {
            get {
                return _paddZone;
            }

            set {
                _paddZone = value;
            }
        }

        public double MilesPerDollar => Vehicle.Mpg / DollarsPerGallon;

        public double DollarsPerGallon {
            get {
                return _dollarsPerGallon;
            }

            set {
                _dollarsPerGallon = value;
            }
        }

        //public override string ToString() {
        //    return Vehicle.ShortDescription;
        //}
    }

    #endregion
}