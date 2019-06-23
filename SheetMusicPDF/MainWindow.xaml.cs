using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;
using Application = System.Windows.Application;
using Image = System.Drawing.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
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

        public Rectangle RestoreRect { get; set; }
        public bool IsMaximized { get; set; }

        private ObservableCollection<BitmapSource> _pdfPageUncroppedImageList;
        private ObservableCollection<BitmapSource> _pdfPageImageList;
        public ObservableCollection<BitmapSource> PDFPageImageList
        {
            get { return _pdfPageImageList; }
            set { _pdfPageImageList = value;
                OnPropertyChanged("PDFPageImageList");
            }
        }

        private bool _isCropping = false;
        public bool IsCropping
        {
            get { return _isCropping; }
            set
            {
                _isCropping = value;
                OnPropertyChanged("IsCropping");
            }
        }

        private double _cropPercentage;
        public double CropPercentage
        {
            get { return _cropPercentage; }
            set
            {
                _cropPercentage = value;
                var ic = (ItemsControl)ScrollPDF.Content;
                var itemWidth = ((ContentPresenter)ic.ItemContainerGenerator.
                    ContainerFromItem(ic.Items[0])).ActualWidth;
                var itemHeight = ((ContentPresenter)ic.ItemContainerGenerator.
                    ContainerFromItem(ic.Items[0])).ActualWidth;
                var hCrop = itemWidth*(_cropPercentage/100.0);
                var vCrop = itemHeight*(_cropPercentage/100.0);
                CropValue = new Thickness(hCrop, vCrop, hCrop, vCrop);
                OnPropertyChanged("CropPercentage");
            }
        }

        private int _pagesToFit = 4;
        public int PagesToFit
        {
            get { return _pagesToFit; }
            set
            {
                _pagesToFit = value;
                OnPropertyChanged("PagesToFit");
            }
        }


        private Thickness _cropValue;
        public Thickness CropValue
        {
            get { return _cropValue; }
            set
            {
                _cropValue = value;
                OnPropertyChanged("CropValue");
            }
        }

        private Thickness _itemsMargin;
        public Thickness ItemsMargin
        {
            get { return _itemsMargin; }
            set
            {
                _itemsMargin = value;
                OnPropertyChanged("ItemsMargin");
            }
        }


        public MainWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) => {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                if (source != null) source.AddHook(WndProc);
                Maximize();
                IsMaximized = true;
                KeyDown += OnKeyDown;
                BuildScalingMenu();
            };
            
        }


        private long lastKeyPressTime = 0;
        private int lastKeyPressNumber = 0;
        private void OnKeyDown(object sender, KeyEventArgs k)
        {
            if(PDFPageImageList != null && PDFPageImageList.Count > 0)
            {
                var ic = (ItemsControl)ScrollPDF.Content;
                var itemWidth = ((ContentPresenter)ic.ItemContainerGenerator.
                    ContainerFromItem(ic.Items[0])).ActualWidth;
                var numItemsShown = Math.Round(ActualWidth/itemWidth);
                var maxScroll = (PDFPageImageList.Count - numItemsShown)*itemWidth;
                // align offset to page
                var offset = Math.Round(ScrollPDF.HorizontalOffset / itemWidth) * itemWidth;

                if (k.Key == Key.Space ||  k.Key == Key.Right || k.Key == Key.Down)
                {
                    ScrollPDF.ScrollToHorizontalOffset(
                        Math.Min(maxScroll, offset + itemWidth));
                }else if  (k.Key == Key.Left || k.Key == Key.Up)
                {
                    ScrollPDF.ScrollToHorizontalOffset(offset - itemWidth);
                }
                else if (k.Key == Key.Home)
                {
                    ScrollPDF.ScrollToLeftEnd();
                }
                else if (k.Key == Key.End)
                {
                    ScrollPDF.ScrollToHorizontalOffset(maxScroll);
                    //ScrollPDF.ScrollToRightEnd();
                }
                else if ((k.Key >= Key.D0 && k.Key <= Key.D9) ||
                    (k.Key >= Key.NumPad0 && k.Key <= Key.NumPad9))
                {
                    int num;
                    if(int.TryParse(k.Key.ToString().First(
                        char.IsDigit).ToString(CultureInfo.InvariantCulture), out num))
                    {
                        var time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        
                        // 2 key within 200ms = double key press  
                        if (time - lastKeyPressTime < 200)
                        { 
                            num += lastKeyPressNumber*10;
                        }
                        if (num != 0) {
                            ScrollPDF.ScrollToHorizontalOffset((num - 1)*itemWidth);
                        }
                        lastKeyPressTime = time;
                        lastKeyPressNumber = num;
                    }
                } 
            }
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

        public ObservableCollection<BitmapSource> GetImages(string inputPdfPath)
        {
            var images = new List<Image>();
            
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
            var converter = new ImageToBitmapSourceConverter();
            return new ObservableCollection<BitmapSource>(
                images.Select(converter.Convert));;
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
            {
                _pdfPageUncroppedImageList = GetImages(openFileDialog.FileName);
                PDFPageImageList = _pdfPageUncroppedImageList;
                MenuCrop.IsEnabled = true;
                UpdateScaling();
            }

        }

        private void BuildScalingMenu()
        {
            MenuScaling.Items.Add(new MenuItem {
                Header = "No Fit (Maximal Scaling)",
                Tag = 0, IsCheckable = true, 
            });
            for(int i = 1; i < 9; i++)
            {
                MenuScaling.Items.Add(new MenuItem{
                    Header = string.Format("Fit {0} page", i) + (i ==1 ? "":"s"),
                    Tag = i,
                    IsCheckable = true
                });
            }
            foreach (MenuItem mi in MenuScaling.Items)
            {
                mi.Click += MiOnClick;
            }
        }

        private void MiOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var m = (MenuItem)sender;
            PagesToFit = (int)m.Tag;
            foreach(MenuItem mi in MenuScaling.Items)
            {
                mi.IsChecked = false;
            }
            m.IsChecked = true;
            UpdateScaling();
        }

        private void UpdateScaling()
        {
      
            if (PDFPageImageList == null || PagesToFit == 0)
            {
                ItemsMargin = new Thickness(0);
                return;
            }

            var image = PDFPageImageList[0];
            var screenWidth = ActualWidth - 2*SystemParameters.ResizeFrameVerticalBorderWidth;
            var screenHeight = ActualHeight - 
                (SystemParameters.WindowCaptionHeight
                + SystemParameters.ResizeFrameHorizontalBorderHeight
                + SystemParameters.HorizontalScrollBarHeight);
            //var screenWidth = PDFContentArea.ActualWidth;
            //var screenHeight = PDFContentArea.ActualHeight;

            var screenAspect = screenWidth/screenHeight;
            var pageWidth = image.PixelWidth;
            var pageHeight = image.PixelHeight;
            var pageAspect = ((double)PagesToFit*pageWidth) / pageHeight;

            var newWidth = screenWidth;
            var newHeight = screenHeight;

            if(screenAspect > pageAspect) // need to reduce width
            {
                newWidth = screenHeight * pageAspect;

            } else // need to reduce height
            {
                newHeight = screenWidth / pageAspect;
            }
            var horiz = (screenWidth - newWidth)/2.0;
            var vert = (screenHeight - newHeight)/2.0;
            ItemsMargin = new Thickness(horiz,vert,horiz,vert);
            
            var ic = (ItemsControl)ScrollPDF.Content;
            var itemWidth = ((ContentPresenter)ic.ItemContainerGenerator.
                ContainerFromItem(ic.Items[0])).ActualWidth;
            if (itemWidth > double.Epsilon)
            {
                var offset = Math.Round(ScrollPDF.HorizontalOffset/itemWidth)*itemWidth;
                ScrollPDF.ScrollToHorizontalOffset(offset);
            }
        }

        private void UpdateCrop()
        {
            var cropped = new ObservableCollection<BitmapSource>();
            var images = _pdfPageUncroppedImageList;
            foreach (var image in images)
            {
                var newW = (int)image.Width - (int)CropValue.Left - (int)CropValue.Right;
                var newH = (int)image.Height - (int)CropValue.Top - (int)CropValue.Bottom;
                cropped.Add(new CroppedBitmap(image, new Int32Rect(
                    (int)CropValue.Left, (int)CropValue.Top, newW, newH)));
            }
            PDFPageImageList = cropped;
            UpdateScaling();
        }

        public void Restore()
        {
            Width = RestoreRect.Width;
            Height = RestoreRect.Height;
            Left = RestoreRect.Left;
            Top = RestoreRect.Top;
        }

        public void Maximize()
        {
            if (Screen.AllScreens.Length > 1)
            {
                RestoreRect = new Rectangle {
                    Width = (int) ActualWidth,
                    Height = (int) ActualHeight,
                    X = (int) Left,
                    Y = (int) Top
                };
                var entireSize = Screen.AllScreens.
                    Aggregate(Rectangle.Empty, (current, s) => 
                        Rectangle.Union(current, s.Bounds));
                Width = entireSize.Width;
                Height = entireSize.Height;
                Left = entireSize.Left;
                Top = entireSize.Top;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
        }

        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MAXIMIZE = 0xF030;
        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND)
            {
                if (wParam.ToInt32() == SC_MAXIMIZE)
                {
                    var hwndSource = HwndSource.FromHwnd(hwnd);
                    if (hwndSource != null)
                    {
                        var window = (MainWindow) hwndSource.RootVisual;
                        if(window.IsMaximized)
                        {
                            window.Restore();
                        }else
                        {
                            window.Maximize();
                        }

                        window.IsMaximized = !window.IsMaximized;
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            IsCropping = true;
            PDFPageImageList = _pdfPageUncroppedImageList;
            // let the uncropped pictures refresh and then show a popup
            //Dispatcher.BeginInvoke(new Action(()=> {
            //if(_cropWindow == null)
           // {
            var popup = new CropWindow(this, CropPercentage);
                
            //}

            popup.ShowDialog();
            IsCropping = false;
            UpdateCrop();
            //}));
        }

        private void PDFWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScaling();
        }
    }




        [ValueConversion(typeof(bool), typeof(Visibility))]
    public class CropToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;

            var isVisible = (bool)value;
            return isVisible ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof (bool), typeof (Visibility))]
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;

            var isVisible = (bool)value;
            return isVisible ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(Image), typeof(BitmapSource))]
    public class ImageToBitmapSourceConverter : IValueConverter
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr value);

        public BitmapSource Convert(Image myImage)
        {
            if (myImage == null) return null;
            try
            {
                var width = myImage.Width;
                var height = myImage.Height;
                Debug.Assert(width > 0 && height > 0);

            } catch(Exception e)
            {
                return null;
            }
            var bitmap = new Bitmap(myImage);
            IntPtr bmpPt = bitmap.GetHbitmap();
            BitmapSource bitmapSource =
             Imaging.CreateBitmapSourceFromHBitmap(
                   bmpPt, IntPtr.Zero, Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());

            //freeze bitmapSource and clear memory to avoid memory leaks
            bitmapSource.Freeze();
            DeleteObject(bmpPt);

            return bitmapSource;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value == null || !(value is Image))
            {
                return null;
            }
            return Convert((Image)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }



    }



}
