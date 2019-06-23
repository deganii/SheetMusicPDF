using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SheetMusicPDF
{
    /// <summary>
    /// Interaction logic for CropWindow.xaml
    /// </summary>
    public partial class CropWindow : INotifyPropertyChanged
    {
        public CropType CropType { get; set; }
        public double CropUniform { get; set; }
        public Point CropHV { get; set; }
        public Thickness CropCustom { get; set; }

        public CropWindow(MainWindow owner)
        {
            CropType = owner.CropType;
            CropUniform = owner.CropUniform;
            CropHV = owner.CropHV;
            CropCustom = owner.CropCustom;
            InitializeComponent();
            Owner = owner;
            radioHV.Tag = CropType.HorizVertical;
            radioUniform.Tag = CropType.Uniform;
            radioCustom.Tag = CropType.Custom;
            Activated += LoadValues;
        }

        private void LoadValues(object sender, EventArgs eventArgs)
        {
            switch (CropType)
            {
                case CropType.Uniform:
                    radioUniform.IsChecked = true;
                    break;
                case CropType.HorizVertical:
                    radioHV.IsChecked = true;
                    break;
                case CropType.Custom:
                    radioCustom.IsChecked = true;
                    break;
            }
            SliderUniform.Value = CropUniform;
            sliderH.Value = CropHV.X;
            sliderV.Value = CropHV.Y;
            sliderTop.Value = CropCustom.Top;
            sliderLeft.Value = CropCustom.Left;
            sliderBottom.Value = CropCustom.Bottom; 
            sliderRight.Value = CropCustom.Right;
        }

        // Create the OnPropertyChanged method to raise the event
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SliderUniform_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ((MainWindow) (Owner)).CropUniform = e.NewValue;
            sliderH.Value = sliderV.Value = e.NewValue;
            sliderLeft.Value = sliderTop.Value =
                 sliderBottom.Value = sliderRight.Value = e.NewValue;
        }

        private void sliderH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var newHV = new Point(e.NewValue, sliderV.Value);
            ((MainWindow)(Owner)).CropHV = newHV;
            sliderLeft.Value = sliderRight.Value = e.NewValue;
            sliderBottom.Value = sliderTop.Value = sliderV.Value;
        }

        private void sliderV_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var newHV = new Point(sliderH.Value, e.NewValue);
            ((MainWindow)(Owner)).CropHV = newHV;
            sliderLeft.Value = sliderRight.Value = sliderH.Value;
            sliderBottom.Value = sliderTop.Value = e.NewValue;
        }

        private void sliderLeft_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var newThickness = new Thickness(
                e.NewValue, sliderTop.Value, sliderRight.Value, sliderBottom.Value);
            ((MainWindow)(Owner)).CropCustom = newThickness;
        }

        private void sliderTop_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var newThickness = new Thickness(
                sliderLeft.Value, e.NewValue, sliderRight.Value, sliderBottom.Value);
            ((MainWindow)(Owner)).CropCustom = newThickness;
        }

        private void sliderRight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var newThickness = new Thickness(
                sliderLeft.Value, sliderTop.Value, e.NewValue, sliderBottom.Value);
            ((MainWindow)(Owner)).CropCustom = newThickness;
        }

        private void sliderBottom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var newThickness = new Thickness(
                sliderLeft.Value, sliderTop.Value, sliderRight.Value, e.NewValue);
            ((MainWindow)(Owner)).CropCustom = newThickness;
        }

        private void radio_Checked(object sender, RoutedEventArgs e)
        {
            ((MainWindow)(Owner)).CropType = (CropType)((RadioButton)sender).Tag;
        }
    }
}
