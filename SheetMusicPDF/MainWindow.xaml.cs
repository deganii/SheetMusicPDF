using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;
using Microsoft.Win32;
using Image = System.Drawing.Image;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace SheetMusicPDF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Size _size;
        private Point _location;
        private int crop = 2;

        private string PdfFile = @"C:\dev\SheetMusicPDF\SheetMusicPDF\Data\IMSLP310090-PMLP02404-Debussy_-_Reverie_pianoviolinscore.pdf";

        private ObservableCollection<Image> _pdfPageImageList;
        public ObservableCollection<Image> PDFPageImageList
        {
            get { return _pdfPageImageList; }
            set { _pdfPageImageList = value;
                OnPropertyChanged("PDFPageImageList");
            }
        }



        private ObservableCollection<Image> _pdfPageCroppedImageList;
        public ObservableCollection<Image> PDFPageCroppedImageList
        {
            get { return _pdfPageCroppedImageList; }
            set
            {
                _pdfPageCroppedImageList = value;
                OnPropertyChanged("PDFPageCroppedImageList");
            }
        }


        public MainWindow()
        {
            InitializeComponent();
            //PDFPageImageList = GetImages();
            //  initialize to fill entire screen
            /* int screenLeft = SystemInformation.VirtualScreen.Left;
             int screenTop = SystemInformation.VirtualScreen.Top;
             int screenWidth = SystemInformation.VirtualScreen.Width;
             int screenHeight = SystemInformation.VirtualScreen.Height;

             this.Size = new System.Drawing.Size(screenWidth, screenHeight);
             this.Location = new System.Drawing.Point(screenLeft, screenTop);*/


        }

        public Point Location
        {
            get { return Location; }
            set { _location = value; }
        }

        public Size Size
        {
            get { return _size; }
            set { _size = value; }
        }

        private GhostscriptVersionInfo _lastInstalledVersion = null;
        private GhostscriptRasterizer _rasterizer = null;

        public ObservableCollection<Image> GetImages(string inputPdfPath)
        {
            var images = new ObservableCollection<Image>();
            
            int desired_x_dpi = 96;
            int desired_y_dpi = 96;

            _lastInstalledVersion =
                GhostscriptVersionInfo.GetLastInstalledVersion(
                    GhostscriptLicense.GPL | GhostscriptLicense.AFPL,
                    GhostscriptLicense.GPL);

            if (_rasterizer == null)
            {
                _rasterizer = new GhostscriptRasterizer();
            }

            _rasterizer.Open(inputPdfPath, _lastInstalledVersion, false);

            for (int pageNumber = 1; pageNumber <= _rasterizer.PageCount; pageNumber++)
            {
                //string pageFilePath = Path.Combine(outputPath, "Page-" + pageNumber.ToString() + ".png");

                var img = _rasterizer.GetPage(desired_x_dpi, desired_y_dpi, pageNumber);
                images.Add(img);

                //img.Save(pageFilePath, ImageFormat.Png);

                //Console.WriteLine(pageFilePath);
            }
            return images;
        }

        // Create the OnPropertyChanged method to raise the event
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                PDFPageImageList = GetImages(openFileDialog.FileName);
            
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            var cropped = new ObservableCollection<Image>();
            var images = PDFPageImageList;
            foreach(Image image in images)
            {
                cropped.Add(CropImage(image, 400,400,100,100));
            }
            PDFPageCroppedImageList = cropped;
        }

        public Image CropImage(Image Image, int Height, int Width, int StartAtX, int StartAtY)
        {
            Image outimage;
            MemoryStream mm = null;
            try
            {
                //check the image height against our desired image height
                if (Image.Height < Height)
                {
                    Height = Image.Height;
                }

                if (Image.Width < Width)
                {
                    Width = Image.Width;
                }

                //create a bitmap window for cropping
                Bitmap bmPhoto = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
                bmPhoto.SetResolution(72, 72);

                //create a new graphics object from our image and set properties
                Graphics grPhoto = Graphics.FromImage(bmPhoto);
                grPhoto.SmoothingMode = SmoothingMode.AntiAlias;
                grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
                grPhoto.PixelOffsetMode = PixelOffsetMode.HighQuality;

                //now do the crop
                grPhoto.DrawImage(Image, new Rectangle(0, 0, Width, Height), StartAtX, StartAtY, Width, Height, GraphicsUnit.Pixel);

                // Save out to memory and get an image from it to send back out the method.
                mm = new MemoryStream();
                bmPhoto.Save(mm, ImageFormat.Jpeg);
                Image.Dispose();
                bmPhoto.Dispose();
                grPhoto.Dispose();
                outimage = Image.FromStream(mm);
                return outimage;
            }
            catch (Exception ex)
            {
                return Image;
                //throw new Exception("Error cropping image, the error was: " + ex.Message);
            }
        }

    }

    [ValueConversion(typeof(Image), typeof(BitmapSource))]
    public class ImageToBitmapSourceConverter : IValueConverter
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr value);

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;

            Image myImage = (Image)value;

            var bitmap = new Bitmap(myImage);
            IntPtr bmpPt = bitmap.GetHbitmap();
            BitmapSource bitmapSource =
             System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                   bmpPt,
                   IntPtr.Zero,
                   Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());

            //freeze bitmapSource and clear memory to avoid memory leaks
            bitmapSource.Freeze();
            DeleteObject(bmpPt);

            return bitmapSource;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
