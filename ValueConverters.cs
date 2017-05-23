#region Value Converters

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;

namespace Esri.APL.MilesPerDollar {

    [ValueConversion(typeof(int), typeof(Visibility))]
    public class CollectionCountToIsVisibleConverter : IValueConverter {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            System.Diagnostics.Debug.WriteLine("CollectionCountToIsVisibleConverter");
            return (int)value > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
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

    [ValueConversion(typeof(string), typeof(string))]
    public class PADDZoneToFuelPriceString : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string ret = "<unavailable>";
            Dictionary<string, double> pz2fc = VehiclesPaneViewModel.Instance.PADDZoneToFuelCost;
            double dVal;
            if (value != null && pz2fc != null && pz2fc.TryGetValue(value as string, out dVal))
                ret = dVal.ToString();
            return ret;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(string), typeof(Color))]
    public class VehicleColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (Color)ColorConverter.ConvertFromString(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return ((Color)value).ToString();
        }
    }
    public class VehicleSolidColorBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value as string));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return ((SolidColorBrush)value).Color.ToString();
        }
    }
    #endregion
}