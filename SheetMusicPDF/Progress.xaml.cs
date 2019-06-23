using System;
using System.Collections.Generic;
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
    /// Interaction logic for Progress.xaml
    /// </summary>
    public partial class Progress : Window
    {
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public Progress(int totalPages)
        {
            TotalPages = totalPages;
            CurrentPage = 0;
            InitializeComponent();
        }

        public void SetPage(int page)
        {
            CurrentPage = page;
            progressBar1.Value = 100.0*page/TotalPages;
            textBlock1.Text = string.Format("Rasterizing Page {0} of {1}", 
                CurrentPage, TotalPages);
        }

    }
}
