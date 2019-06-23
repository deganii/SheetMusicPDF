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
        private int _cropValue;
        public int CropValue
        {
            get { return _cropValue; }
            set
            {
                _cropValue = value;
                OnPropertyChanged("CropValue");
            }
        }

        public CropWindow(Window owner)
        {
            InitializeComponent();
            Owner = owner;
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

        private void SliderCrop_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ((MainWindow) (Owner)).CropValue = (int)e.NewValue;
        }
    }
}
