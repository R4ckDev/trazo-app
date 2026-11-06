using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;

namespace Trazo
{
    internal sealed class BarcodeReadResult
    {
        internal string Format;
        internal string Text;
    }

    internal static class BarcodeDecoder
    {
        private const long MaximumPixels=24000000;
        private static readonly BarcodeFormat[] Formats={BarcodeFormat.QR_CODE,BarcodeFormat.DATA_MATRIX,BarcodeFormat.AZTEC,BarcodeFormat.PDF_417,BarcodeFormat.CODE_128,BarcodeFormat.CODE_39,BarcodeFormat.CODE_93,BarcodeFormat.EAN_13,BarcodeFormat.EAN_8,BarcodeFormat.UPC_A,BarcodeFormat.UPC_E,BarcodeFormat.ITF,BarcodeFormat.CODABAR};
        internal static BarcodeReadResult[] Decode(Bitmap image)
        {
            if(image==null)throw new ArgumentNullException("image");
            if((long)image.Width*image.Height>MaximumPixels)throw new ArgumentException("La imagen es demasiado grande. Selecciona una zona más pequeña alrededor de los códigos.","image");
            List<BarcodeReadResult> results=new List<BarcodeReadResult>();HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
            using(Bitmap normalized=Normalize(image))
            {
                Read(normalized,results,seen);
                // Enlarging a small crop can help the binarizer, without altering the source pixels.
                if(results.Count==0&&normalized.Width<=1000&&normalized.Height<=1000)
                {
                    using(Bitmap enlarged=new Bitmap(normalized.Width*2,normalized.Height*2,PixelFormat.Format24bppRgb))
                    {
                        using(Graphics g=Graphics.FromImage(enlarged)){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(normalized,new Rectangle(Point.Empty,enlarged.Size),new Rectangle(Point.Empty,normalized.Size),GraphicsUnit.Pixel);}
                        Read(enlarged,results,seen);
                    }
                }
            }
            return results.ToArray();
        }
        private static Bitmap Normalize(Bitmap source)
        {
            Bitmap result=new Bitmap(source.Width,source.Height,PixelFormat.Format24bppRgb);
            try{using(Graphics g=Graphics.FromImage(result)){g.Clear(Color.White);g.DrawImageUnscaled(source,0,0);}return result;}catch{result.Dispose();throw;}
        }
        private static void Read(Bitmap image,List<BarcodeReadResult> results,HashSet<string> seen)
        {
            BarcodeReader reader=Reader(Formats);
            Append(reader.DecodeMultiple(image),results,seen);
            // A separate QR pass also finds several QR codes beside a linear barcode.
            Append(Reader(new BarcodeFormat[]{BarcodeFormat.QR_CODE}).DecodeMultiple(image),results,seen);
            if(results.Count==0){Result single=reader.Decode(image);if(single!=null)Append(new Result[]{single},results,seen);}
        }
        private static BarcodeReader Reader(IList<BarcodeFormat> formats)
        {return new BarcodeReader{AutoRotate=true,Options=new DecodingOptions{TryHarder=true,TryInverted=true,PossibleFormats=formats}};}
        private static void Append(Result[] decoded,List<BarcodeReadResult> results,HashSet<string> seen)
        {
            if(decoded==null)return;
            foreach(Result result in decoded)
            {
                if(result==null||result.Text==null)continue;
                string key=result.BarcodeFormat+"\0"+result.Text;if(!seen.Add(key))continue;
                results.Add(new BarcodeReadResult{Format=FormatName(result.BarcodeFormat),Text=result.Text});
            }
        }
        private static string FormatName(BarcodeFormat format)
        {
            switch(format)
            {
                case BarcodeFormat.QR_CODE:return "QR";case BarcodeFormat.DATA_MATRIX:return "Data Matrix";case BarcodeFormat.PDF_417:return "PDF417";
                case BarcodeFormat.CODE_128:return "Code 128";case BarcodeFormat.CODE_39:return "Code 39";case BarcodeFormat.CODE_93:return "Code 93";
                case BarcodeFormat.EAN_13:return "EAN-13";case BarcodeFormat.EAN_8:return "EAN-8";case BarcodeFormat.UPC_A:return "UPC-A";case BarcodeFormat.UPC_E:return "UPC-E";
                default:return format.ToString();
            }
        }
    }
}
