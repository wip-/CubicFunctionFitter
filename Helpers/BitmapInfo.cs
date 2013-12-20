using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CubicFunctionFitter
{
    public class BitmapInfo
    {
        public int Width;
        public int Height;
        public int Stride;
        public int DataByteSize;
        public byte[] Data;
        public PixelFormat PixelFormat;
        public int BitsPerPixel;
        public int BytesPerPixel;
        private ColorPalette palette;

        public enum CopyData
        {
            True,
            False
        }

        static public int GetBytesPerPixel(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                    return 1;

                case PixelFormat.Format24bppRgb:
                    return 3;

                case PixelFormat.Format32bppArgb:
                    return 4;

                case PixelFormat.Format64bppArgb:
                    return 8;

                default:
                    Debug.Assert(false);
                    return 0;
            }
        }

        static public bool HasAlphaChannel(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                case PixelFormat.Format24bppRgb:
                    return false;

                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format64bppArgb:
                    return true;

                default:
                    Debug.Assert(false);
                    return false;
            }
        }

        public Boolean HasAlphaChannel()
        {
            return HasAlphaChannel(PixelFormat);
        }

        public BitmapInfo(Bitmap bitmap, CopyData copyData = CopyData.True)
        {
            Width = bitmap.Width;
            Height = bitmap.Height;
            PixelFormat = bitmap.PixelFormat;
            BitsPerPixel = Image.GetPixelFormatSize(PixelFormat);
            BytesPerPixel = GetBytesPerPixel(bitmap.PixelFormat);
            Rectangle rect = Rectangle.FromLTRB(0, 0, Width, Height);
            BitmapData bitmapData = bitmap.LockBits(rect,
                ImageLockMode.ReadOnly, PixelFormat);
            Stride = Math.Abs(bitmapData.Stride);
            DataByteSize = Stride * Height;
            Data = new byte[DataByteSize];
            if (copyData == CopyData.True)
                Marshal.Copy(bitmapData.Scan0, Data, 0, DataByteSize);
            bitmap.UnlockBits(bitmapData);

            if (PixelFormat == PixelFormat.Format8bppIndexed)
                palette = bitmap.Palette;
        }

        public BitmapInfo(int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            BitsPerPixel = Image.GetPixelFormatSize(PixelFormat);
            BytesPerPixel = GetBytesPerPixel(PixelFormat);

            int bitsPerLine = BytesPerPixel * Width;
            double d = (bitsPerLine+3) / 4;
            double d_rounded = Math.Ceiling(d);
            Stride = 4 * Convert.ToInt32(d_rounded);

            DataByteSize = Stride * Height;
            Data = new byte[DataByteSize];
        }


        public Bitmap ToBitmap()
        {
            Bitmap bitmap = new Bitmap(Width, Height);
            Rectangle rect = Rectangle.FromLTRB(0, 0, Width, Height);
            BitmapData bitmapData = bitmap.LockBits(rect,
                ImageLockMode.WriteOnly, PixelFormat);
            Marshal.Copy(Data, 0, bitmapData.Scan0, DataByteSize);
            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }


        // Get index of pixel in Data array from its coordinates
        private Int32 GetPixelInternalIndex(int x, int y)
        {
            return (Stride * y) + (BytesPerPixel * x);
        }

        public Color GetPixelColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return Color.FromArgb(0, 0, 0, 0);

            int pixelIndex = GetPixelInternalIndex(x, y);
            
            if (PixelFormat == PixelFormat.Format8bppIndexed)
            {
                byte colorIndex = Data[pixelIndex];
                return palette.Entries[colorIndex];
            }
            else
            {
                byte A = HasAlphaChannel() ? Data[pixelIndex + 3] : (byte)255;
                byte R = Data[pixelIndex + 2];
                byte G = Data[pixelIndex + 1];
                byte B = Data[pixelIndex + 0];

                return Color.FromArgb(A, R, G, B);
            }
        }

        public void SetPixelColor(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;

            int index = GetPixelInternalIndex(x, y);
            Data[index + 0] = color.B;  // B
            Data[index + 1] = color.G;  // G
            Data[index + 2] = color.R;  // R
            if (HasAlphaChannel())
                Data[index + 3] = color.A;  // A
        }

        // blends RGB with color at pixel (x,y) with formula:
        // newPixelColor = (1-ratio)*oldPixelColor + ratio*color
        public void BlendPixelColor(int x, int y, Color color, double ratio)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || ratio==0)
                return;

            int index = GetPixelInternalIndex(x, y);
            Color oldColor = GetPixelColor(x, y);
            double r = (1 - ratio) * oldColor.R + ratio * color.R;
            double g = (1 - ratio) * oldColor.G + ratio * color.G;
            double b = (1 - ratio) * oldColor.B + ratio * color.B;
            Color newColor = Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
            SetPixelColor(x, y, newColor);
        }



        // http://en.wikipedia.org/wiki/Bresenham%27s_line_algorithm
        public void DrawBresenhamLine(int x0, int y0, int x1, int y1, Color color)
        {
            int deltax = x1 - x0;
            int deltay = y1 - y0;
            double error = 0;

            // Assume deltax != 0 (line is not vertical),
            // note that this division needs to be done in a way that preserves the fractional part
            double deltaerr = Math.Abs((double)deltay / deltax);    
                                                            
            int y = y0;
            for(int x = x0; x < x1; ++x)
            {
                SetPixelColor(x, y, color);
                error += deltaerr;
                if(error >= 0.5)
                {
                    ++y;
                    --error;
                }
            }
        }



        
        // methods for xiaolin wu
        static private double ipart(double x)
        {
            return Math.Ceiling(x);
        }
        static private double round(double x)
        {
            return ipart(x+0.5);
        }
        static private double fpart(double x)
        {
            return x-ipart(x);
        }
        static private double rfpart(double x)
        {
            return 1-fpart(x);
        }
        static private void swap(ref double a, ref double b)
        {
            double c = a;
            a = b;
            b = c;
        }



        public void DrawPoint(double x0, double y0, Color color)
        {
            double weightXleft   = 0;   // weight for pixel on the left   ipart(x0)-1
            double weightXmiddle = 0;   // weight for pixel on the middle ipart(x0)
            double weightXright  = 0;   // weight for pixel on the right  ipart(x0)+1
            if (fpart(x0) > 0.5)
            {
                weightXright  = fpart(x0) - 0.5;
                weightXmiddle = 1 - weightXright;
            }
            else if (fpart(x0) < 0.5)
            {
                weightXleft   = 0.5 - fpart(x0);
                weightXmiddle = 1 - weightXleft;
            }
            else if (fpart(x0) == 0.5)
            {
                weightXmiddle = 1;
            }

            double weightYtop = 0;      // weight for pixel on the top    ipart(y0)-1
            double weightYmiddle = 0;   // weight for pixel on the middle ipart(y0)
            double weightYbottom = 0;   // weight for pixel on the bottom ipart(y0)+1
            if (fpart(y0) > 0.5)
            {
                weightYbottom= fpart(x0) - 0.5;
                weightYmiddle = 1 - weightYbottom;
            }
            else if (fpart(y0) < 0.5)
            {
                weightYtop = 0.5 - fpart(x0);
                weightYmiddle = 1 - weightYtop;
            }
            else if (fpart(y0) == 0.5)
            {
                weightYmiddle = 1;
            }

            BlendPixelColor( (int)ipart(x0) - 1, (int)ipart(y0) - 1, color, weightXleft * weightYtop);
            BlendPixelColor( (int)ipart(x0) - 1, (int)ipart(y0) + 0, color, weightXleft * weightYmiddle);
            BlendPixelColor( (int)ipart(x0) - 1, (int)ipart(y0) + 1, color, weightXleft * weightYbottom);
                             
            BlendPixelColor( (int)ipart(x0) + 0, (int)ipart(y0) - 1, color, weightXmiddle * weightYtop);
            BlendPixelColor( (int)ipart(x0) + 0, (int)ipart(y0) + 0, color, weightXmiddle * weightYmiddle);
            BlendPixelColor( (int)ipart(x0) + 0, (int)ipart(y0) + 1, color, weightXmiddle * weightYbottom);
                             
            BlendPixelColor( (int)ipart(x0) + 1, (int)ipart(y0) - 1, color, weightXright * weightYtop);
            BlendPixelColor( (int)ipart(x0) + 1, (int)ipart(y0) + 0, color, weightXright * weightYmiddle);
            BlendPixelColor( (int)ipart(x0) + 1, (int)ipart(y0) + 1, color, weightXright * weightYbottom);
        }


        // http://en.wikipedia.org/wiki/Xiaolin_Wu's_line_algorithm
        public void DrawXiaolinWuLine(double x0, double y0, double x1, double y1, Color color)
        {
            //x0.Clamp(0, Width-1);
            //x1.Clamp(0, Width-1);
            //y0.Clamp(0, Height-1);
            //y1.Clamp(0, Height-1);

            bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
            if(steep)
            {
                swap(ref x0, ref y0);
                swap(ref x1, ref y1);
            }

            if( x0 > x1)
            {
                swap(ref x0, ref x1);
                swap(ref y0, ref y1);
            }
 
            double dx = x1 - x0;
            double dy = y1 - y0;
            double gradient = dy / dx;


            // handle first endpoint
            int xend = (int)round(x0);
            double yend = y0 + gradient * (xend - x0);
            double xgap = rfpart(x0 + 0.5);
            int xpxl1 = xend;   //this will be used in the main loop
            int ypxl1 = (int)ipart(yend);

            if(steep)
            {
                BlendPixelColor(ypxl1,   xpxl1, color, rfpart(yend) * xgap);
                BlendPixelColor(ypxl1+1, xpxl1, color,  fpart(yend) * xgap);
            }
            else
            {
                BlendPixelColor(xpxl1, ypxl1  , color, rfpart(yend) * xgap);
                BlendPixelColor(xpxl1, ypxl1+1, color,  fpart(yend) * xgap);
            }
            double intery = yend + gradient;  // first y-intersection for the main loop
 
     
            // handle second endpoint
            xend = (int)round(x1);
            yend = y1 + gradient * ((double)xend - x1);
            xgap = fpart(x1 + 0.5);
            int xpxl2 = xend; //this will be used in the main loop
            int ypxl2 = (int)ipart(yend);
            if(steep)
            {
                BlendPixelColor(ypxl2  , xpxl2, color, rfpart(yend) * xgap);
                BlendPixelColor(ypxl2+1, xpxl2, color,  fpart(yend) * xgap);
            }
            else
            {
                BlendPixelColor(xpxl2, ypxl2,  color, rfpart(yend) * xgap);
                BlendPixelColor(xpxl2, ypxl2+1, color, fpart(yend) * xgap);
            }

            // main loop
            for(int x = xpxl1 + 1; x< xpxl2; ++x)
            {
                if( steep )
                {
                    BlendPixelColor((int)ipart(intery)    , x, color, rfpart(intery));
                    BlendPixelColor((int)ipart(intery) + 1, x, color,  fpart(intery));
                }
                else
                {
                    BlendPixelColor(x, (int)ipart(intery)    , color, rfpart(intery));
                    BlendPixelColor(x, (int)ipart(intery) + 1, color,  fpart(intery));
                }
                intery = intery + gradient;
            }
        }

    }
}
