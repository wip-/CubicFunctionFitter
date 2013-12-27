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
using MathNet.Numerics;

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
                ScrollViewerSampleValues,
                ScrollViewerCurveYValues,
                ScrollViewerYErrors,
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
            UpdateCoefficients();
        }

        private void TextBoxYValues_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCoefficients();
        }

        private void TextBoxSampleValues_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCoefficients();
        }

        private void ComboBoxDegrees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCoefficients();
        }

        // return non-null string in case of error
        private String GetXYinputValues(out double[] xValues, out double[] yValues)
        {
            if (TextBoxXValues != null && TextBoxYValues != null && 
                !String.IsNullOrEmpty(TextBoxXValues.Text) && !String.IsNullOrEmpty(TextBoxXValues.Text))
            {
                string[] xValuesString = TextBoxXValues.Text.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                string[] yValuesString = TextBoxYValues.Text.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                xValues = Array.ConvertAll(xValuesString, Double.Parse);
                yValues = Array.ConvertAll(yValuesString, Double.Parse);

                if (xValues.Count() != yValues.Count())
                    return "X and Y values size are not the same";

                if (xValues.Count() == 0)
                    return "Still an empty column";

                if (xValues.Count() < GetMinSampleSize())
                    return "Not enough values";

                // success
                return null;
            }

            xValues = new double[0];
            yValues = new double[0];
            return Resources["LabelInfoInitialValue"].ToString();
        }


        private int GetPolynomialOrder()
        {
            if (ComboBoxDegrees.SelectedItem == ComboBoxItem3Degrees) return 3;
            if (ComboBoxDegrees.SelectedItem == ComboBoxItem2Degrees) return 2;
            if (ComboBoxDegrees.SelectedItem == ComboBoxItem1Degrees) return 1;

            Debug.Assert(false);
            return Int32.MaxValue;
        }

        private int GetMinSampleSize()
        {
            return GetPolynomialOrder() + 1;
        }

        // return non-null string in case of error
        private String SetSampleWeightsString(String value)
        {
            double[] xValues, yValues;
            String msg = GetXYinputValues(out xValues, out yValues);
            if (msg != null)
                return msg;

            String sampleValues = String.Empty;
            for (int i = 0; i < xValues.Length; ++i)
            {
                sampleValues += value + "\n";
            }
            TextBoxSampleValues.Text = sampleValues;

            return null;
        }

        private void ButtonSampleAll_Click(object sender, RoutedEventArgs e)
        {
            String msg = SetSampleWeightsString("1");
            if(msg!=null)
                LabelInfo.Text = msg;
        }


        private void ButtonSampleNone_Click(object sender, RoutedEventArgs e)
        {
            String msg = SetSampleWeightsString("0");
            if (msg != null)
                LabelInfo.Text = msg;
        }

        private void ButtonSampleMin_Click(object sender, RoutedEventArgs e)
        {
            double[] xValues, yValues;
            String msg = GetXYinputValues(out xValues, out yValues);
            if (msg != null)
                LabelInfo.Text = msg;

            List<int> sampleIndexes = new List<int>();
            sampleIndexes.Add(0);                       // first
            sampleIndexes.Add(xValues.Length-1);        // last
            int splitsCount = GetMinSampleSize() - 1;
            for (int i = 1; i < splitsCount; ++i)
            {
                sampleIndexes.Add((int)(i * (double)xValues.Length / splitsCount));
            }

            String sampleValues = String.Empty;
            for (int i = 0; i < xValues.Length; ++i)
            {
                if(sampleIndexes.Contains(i))
                    sampleValues += "1\n";
                else
                    sampleValues += "0\n";
            }
            TextBoxSampleValues.Text = sampleValues;
        }

        private void UpdateCoefficients()
        {
            String res = UpdateCoefficientsSub();
            if(res!=null && LabelInfo!=null)
                LabelInfo.Text = res;
        }

        private String UpdateCoefficientsSub()
        {
            double[] xValuesAll, yValuesAll;
            String msg = GetXYinputValues(out xValuesAll, out yValuesAll);
            if (msg != null)
                return msg;

            // initialize sample weights
            if (String.IsNullOrEmpty(TextBoxSampleValues.Text))
            {
                ButtonSampleAll_Click(null, null);
            }

            string[] sampleValuesString = TextBoxSampleValues.Text.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            double[] sampleValues = Array.ConvertAll(sampleValuesString, Double.Parse);
            if (xValuesAll.Count() != sampleValues.Count())
                return "X and sample values size are not the same";

            List<double> xValuesList = new List<double>();
            List<double> yValuesList = new List<double>();

            for (int i = 0; i < sampleValues.Length; ++i )
            {
                if (sampleValues[i] == 1)
                {
                    xValuesList.Add(xValuesAll[i]);
                    yValuesList.Add(yValuesAll[i]);
                }
            }

            if (xValuesList.Count < GetMinSampleSize())
                return "Not enough samples";

            double[] xValues = xValuesList.ToArray();
            double[] yValues = yValuesList.ToArray();

            // http://numerics.mathdotnet.com/api/MathNet.Numerics/Fit.htm
            double[] p = Fit.Polynomial(xValues, yValues, GetPolynomialOrder());
            Array.Reverse(p);


            TextBox[] textBoxes = new TextBox[]
            {
                TextBoxResultA, 
                TextBoxResultB, 
                TextBoxResultC, 
                TextBoxResultD
            };

            for (int i = 0; i < 4; ++i)
                textBoxes[i].Text = String.Empty;

            for (int i = 0; i <= GetPolynomialOrder(); ++i )
            {
                textBoxes[i].Text = String.Format("{0:F15}", p[i]);
            }


            return "Coefficients obtained";
        }





        private void UpdateErrorValues(double[] xValuesAll, double[] yValuesAll, double[] coefficients)
        {
            String yCurveValues = String.Empty;
            String yErrors = String.Empty;
            double totalError = 0;
            for (int i = 0; i < xValuesAll.Length; ++i)
            {
                double yCurveValue = GetCurveValue(xValuesAll[i], coefficients);
                double error = yCurveValue - yValuesAll[i];
                totalError += error * error;
                yCurveValues += String.Format("{0:F7}\n", yCurveValue);
                yErrors += String.Format("{0:F7}\n", error);
            }
            TextBoxCurveYValues.Text = yCurveValues;
            TextBoxYErrors.Text = yErrors;
            LabelAverageError.Content = String.Format("{0:F7}", Math.Sqrt(totalError / xValuesAll.Length));
        }




        private void UpdateGraph(double[] xValuesAll, double[] yValuesAll, double[] coefficients)
        {
            Bitmap graph = GetGraph(xValuesAll, yValuesAll, coefficients);

            ImageGraph.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    graph.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            ButtonResetZoom_Click(null, null);
        }

        private double GetCurveValue(double x, double[] coefficients)
        {
            double result = 0;
            int polynomialDegree = coefficients.Length - 1;
            for (int i = 0; i <= polynomialDegree; ++i)
            {
                result += coefficients[i] * Math.Pow(x, polynomialDegree-i);
            }
            return result;
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
            // TODO: display values or some other cool thing
        }


        private String GetChartFirstColumn(List<string[]> data)
        {
            String str = String.Empty;
            foreach (string[] stringsLine in data)
            {
                str += stringsLine[0] + "\n";
            }
            return str;
        }

        private void PasteChartTwoFirstColumns(List<string[]> data)
        {
            String str0= String.Empty;
            String str1 = String.Empty;
            foreach (string[] stringsLine in data)
            {
                str0 += stringsLine[0] + "\n";
                str1 += stringsLine[1] + "\n";
            }
            TextBoxXValues.Text = str0;
            TextBoxYValues.Text = str1;
            UpdateCoefficients();
        }


        private void ScrollViewerXValues_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            List<string[]> data = ClipboardHelper.ParseClipboardData();
            if (data[0].Length > 1)
            {
                PasteChartTwoFirstColumns(data);
            }
            else
            {
                TextBoxXValues.Text = GetChartFirstColumn(data);
            }
        }

        private void ScrollViewerYValues_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            List<string[]> data = ClipboardHelper.ParseClipboardData();
            if (data[0].Length > 1)
            {
                PasteChartTwoFirstColumns(data);
            }
            else
            {
                TextBoxYValues.Text = GetChartFirstColumn(data);
            }
        }

        private void SliderZoomOut_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderZoomOut.IsInitialized && SliderZoomOut.Value != 1)
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

        private void TextBoxCoefficient_TextChanged(object sender, TextChangedEventArgs e)
        {
            double[] xValuesAll, yValuesAll;
            String msg = GetXYinputValues(out xValuesAll, out yValuesAll);
            if (msg != null)
                LabelInfo.Text = msg;

            List<double> coefficients = new List<double>();
            if (!String.IsNullOrEmpty(TextBoxResultA.Text)) coefficients.Add(Convert.ToDouble(TextBoxResultA.Text));
            if (!String.IsNullOrEmpty(TextBoxResultB.Text)) coefficients.Add(Convert.ToDouble(TextBoxResultB.Text));
            if (!String.IsNullOrEmpty(TextBoxResultC.Text)) coefficients.Add(Convert.ToDouble(TextBoxResultC.Text));
            if (!String.IsNullOrEmpty(TextBoxResultD.Text)) coefficients.Add(Convert.ToDouble(TextBoxResultD.Text));

            if (coefficients.Count == GetPolynomialOrder() + 1)
            {
                UpdateErrorValues(xValuesAll, yValuesAll, coefficients.ToArray());
                UpdateGraph(xValuesAll, yValuesAll, coefficients.ToArray());
            }
        }




    }
}
