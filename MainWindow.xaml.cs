using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CubicFunctionFitter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataObject.AddPastingHandler(TextBoxXValues, ScrollViewerXValues_OnPaste);
            DataObject.AddPastingHandler(TextBoxYValues, ScrollViewerYValues_OnPaste);
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            double vert = ((ScrollViewer)sender).VerticalOffset;
            double hori = ((ScrollViewer)sender).HorizontalOffset;

            ScrollViewer[] scrollViewers = new ScrollViewer[]
            {
                ScrollViewerXValues, 
                ScrollViewerYValues, 
                ScrollViewerCurveYValues, 
            };

            foreach (ScrollViewer scrollViewer in scrollViewers)
            {
                if (scrollViewer == null)
                    continue;
                scrollViewer.ScrollToVerticalOffset(vert);
                scrollViewer.ScrollToHorizontalOffset(hori);
                scrollViewer.UpdateLayout();
            }
        }

        private void TextBoxXValues_TextChanged(object sender, TextChangedEventArgs e)
        {
            Update();
        }

        private void TextBoxYValues_TextChanged(object sender, TextChangedEventArgs e)
        {
            Update();
        }

        private void Update()
        {
            String res = UpdateSub();
            if(res!=null)
                LabelInfo.Text = res;
        }

        private String UpdateSub()
        {
            if (!String.IsNullOrEmpty(TextBoxXValues.Text) && !String.IsNullOrEmpty(TextBoxXValues.Text))
            {
                string[] xValuesString = TextBoxXValues.Text.Split(new string[] {"\n", "\r\n"}, StringSplitOptions.RemoveEmptyEntries);
                string[] yValuesString = TextBoxYValues.Text.Split(new string[] {"\n", "\r\n"}, StringSplitOptions.RemoveEmptyEntries);
                double[] xValues = Array.ConvertAll(xValuesString, Double.Parse);
                double[] yValues = Array.ConvertAll(yValuesString, Double.Parse);

                if (xValues.Count() != yValues.Count())
                    return "X and Y values size are not the same";

                if (xValues.Count() == 0)
                    return "Still an empty column";

                if (xValues.Count() < 4)
                    return "Not enough values";

                // http://christoph.ruegg.name/blog/linear-regression-mathnet-numerics.html

                // build matrices
                var X = DenseMatrix.OfColumnVectors(
                    new[]
                    {
                        DenseVector.Create(xValues.Length, t=>t*t*t),
                        DenseVector.Create(xValues.Length, t=>t*t),
                        DenseVector.Create(xValues.Length, t=>t),
                        DenseVector.Create(xValues.Length, t=>1)
                    });

                var y = new DenseVector(yValues);

                // solve
                var p = X.QR().Solve(y);


                //TextBoxResultA.Text = String.Format("{0:#########.###############}", p.ToArray()[0]);
                //TextBoxResultB.Text = String.Format("{0:#########.###############}", p.ToArray()[1]);
                //TextBoxResultC.Text = String.Format("{0:#########.###############}", p.ToArray()[2]);
                //TextBoxResultD.Text = String.Format("{0:#########.###############}", p.ToArray()[3]);

                TextBoxResultA.Text = String.Format("{0:F15}", p.ToArray()[0]);
                TextBoxResultB.Text = String.Format("{0:F15}", p.ToArray()[1]);
                TextBoxResultC.Text = String.Format("{0:F15}", p.ToArray()[2]);
                TextBoxResultD.Text = String.Format("{0:F15}", p.ToArray()[3]);

                //double[] yCurveValues = new double[xValues.Length];
                String yCurveValues = String.Empty;
                for (int i = 0; i < xValues.Length; ++i )
                {
                    double yCurveValue = GetCurveValue(xValues[i], p.ToArray());
                    //yCurveValues[i] = yCurveValue;
                    yCurveValues += yCurveValue + "\n";
                }
                TextBoxCurveYValues.Text = yCurveValues;


                //TextBoxResultA.Text = p.ToArray()[0].ToString();
                //TextBoxResultB.Text = p.ToArray()[1].ToString();
                //TextBoxResultC.Text = p.ToArray()[2].ToString();
                //TextBoxResultD.Text = p.ToArray()[3].ToString();


                Bitmap graph = GetGraph(xValues, yValues, p.ToArray());

                ImageGraph.Source =
                    Imaging.CreateBitmapSourceFromHBitmap(
                        graph.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                ButtonResetZoom_Click(null, null);

                return "Success";
            }

            return Resources["LabelInfoInitialValue"].ToString();
        }


        private double GetCurveValue(double x, double[] coefficients)
        {
            return
                coefficients[0] * x * x * x +
                coefficients[1] * x * x +
                coefficients[2] * x * +
                coefficients[3];
        }

        private Bitmap GetGraph(double[] xValues, double[] yValues, double[] coefficients)
        {
            // Values mapped to the bitmap corners - we add a 5% margin
            double xValueMin = xValues.Min() - 0.05 * (xValues.Max() - xValues.Min());
            double xValueMax = xValues.Max() + 0.05 * (xValues.Max() - xValues.Min());
            double yValueMin = yValues.Min() - 0.05 * (yValues.Max() - yValues.Min());
            double yValueMax = yValues.Max() + 0.05 * (yValues.Max() - yValues.Min());

            int width  = 360;
            int height = 360;
            BitmapInfo bitmapInfo = new BitmapInfo(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);


            // Render background (in white)

            for (int x = 0; x < width; ++x)
                for (int y = 0; y < height; ++y)
                    bitmapInfo.SetPixelColor(x, y, System.Drawing.Color.White);

            // TODO fix colors
            
            // Render fitted curve (in red)

            double[] yCurveCoordinates = new double[width];
            // resolution : one point per pixel (we could use less)
            for (int x = 0; x <= width - 1; ++x )
            {
                // value in the range of xValues data
                double xValue = Helpers.Lerp(x, 0, (width - 1), xValueMin, xValueMax);
                double yValue = GetCurveValue(xValue, coefficients);
                yCurveCoordinates[x] = Helpers.Lerp(yValue, yValueMin, yValueMax, (height - 1), 0);
            }
            for (int x = 0; x < width - 1; ++x)
            {
                bitmapInfo.DrawXiaolinWuLine(
                    x    , yCurveCoordinates[x    ],
                    x + 1, yCurveCoordinates[x + 1],
                    System.Drawing.Color.Red);
            }

            // TODO points more visible

            // Render data points (blue, or purple if overlapping curve)
            for (int i = 0; i < xValues.Count(); ++i)
            {
                double x = Helpers.Lerp(xValues[i], xValueMin, xValueMax, 0, (width - 1));
                double y = Helpers.Lerp(yValues[i], yValueMin, yValueMax, (height - 1), 0);

                bitmapInfo.DrawPoint(x, y, System.Drawing.Color.Blue);
            }



            // purple : overlapping pixels


            return bitmapInfo.ToBitmap();
        }


        private void ImageGraph_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void ScrollViewerXValues_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();

            List<string[]> data = ClipboardHelper.ParseClipboardData();
            String str = String.Empty;
            foreach (string[] stringsLine in data)
            {
                str += stringsLine[0] + "\n";
            }
            TextBoxXValues.Text = str;
        }

        private void ScrollViewerYValues_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();

            List<string[]> data = ClipboardHelper.ParseClipboardData();
            String str = String.Empty;
            foreach (string[] stringsLine in data)
            {
                str += stringsLine[0] + "\n";
            }
            TextBoxYValues.Text = str;
        }

        private void SliderZoomOut_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderZoomOut.Value != 1)
                Zoom(SliderZoomOut.Value);
            if (SliderZoomIn != null)
                SliderZoomIn.Value = 1;
        }

        private void SliderZoomIn_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderZoomIn.Value != 1)
                Zoom(SliderZoomIn.Value);
            if (SliderZoomOut != null)
                SliderZoomOut.Value = 1;
        }

        private void ButtonResetZoom_Click(object sender, RoutedEventArgs e)
        {
            Zoom(1);
            SliderZoomIn.Value = 1;
            SliderZoomOut.Value = 1;
        }

        private double GetCurrentZoom()
        {
            if (SliderZoomIn.Value != 1)
                return SliderZoomIn.Value;
            if (SliderZoomOut.Value != 1)
                return SliderZoomIn.Value;
            return 1;
        }

        private void Zoom(double val)
        {
            try
            {
                if (ImageGraph == null)
                        return;

                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = val;
                myScaleTransform.ScaleX = val;
                if (LabelZoom != null)
                    LabelZoom.Content = val;
                TransformGroup myTransformGroup = new TransformGroup();
                myTransformGroup.Children.Add(myScaleTransform);

                ImageGraph.LayoutTransform = myTransformGroup;
            }
            catch (System.Exception ex)
            {
                MyCatch(ex);
            }
        }

        private void MyCatch(System.Exception ex)
        {
            var st = new StackTrace(ex, true);      // stack trace for the exception with source file information
            var frame = st.GetFrame(0);             // top stack frame
            String sourceMsg = String.Format("{0}({1})", frame.GetFileName(), frame.GetFileLineNumber());
            Console.WriteLine(sourceMsg);
            MessageBox.Show(ex.Message + Environment.NewLine + sourceMsg);
            Debugger.Break();
        }
    }
}
