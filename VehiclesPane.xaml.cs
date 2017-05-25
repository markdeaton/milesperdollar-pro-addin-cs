using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace Esri.APL.MilesPerDollar {
    /// <summary>
    /// Interaction logic for VehiclesPaneView.xaml
    /// </summary>
    public partial class VehiclesPaneView : UserControl {
        public VehiclesPaneView() {
            InitializeComponent();
        }

        private void AboutFuelPriceData_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            System.Diagnostics.Process.Start("https://www.eia.gov/petroleum/");
        }

        private void AboutVehicleData_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            System.Diagnostics.Process.Start("http://www.fueleconomy.gov/feg/info.shtml");
        }
    }
}
