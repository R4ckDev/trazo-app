using System;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Trazo
{
    internal static class BarcodeDecoderTests
    {
        private static int count;
        private static void Check(bool condition,string label){count++;if(!condition)throw new Exception(label);}
        private static bool Has(BarcodeReadResult[] results,string format,string text){foreach(BarcodeReadResult result in results)if(result.Format==format&&result.Text==text)return true;return false;}
        internal static Bitmap Code(BarcodeFormat format,string text,int width,int height)
        {
            EncodingOptions options=format==BarcodeFormat.QR_CODE?(EncodingOptions)new QrCodeEncodingOptions{Width=width,Height=height,Margin=4,CharacterSet="UTF-8"}:new EncodingOptions{Width=width,Height=height,Margin=20};
            return new BarcodeWriter{Format=format,Options=options}.Write(text);
        }
        internal static int Run()
        {
            count=0;string url="https://example.com/trazo?prueba=1",unicode="Trazo áéíóú ñ\nSegunda línea";
            using(Bitmap qr=Code(BarcodeFormat.QR_CODE,url,240,240))
            {
                Color before=qr.GetPixel(0,0);Check(Has(BarcodeDecoder.Decode(qr),"QR",url),"QR text is read exactly");
                Check(qr.Size==new Size(240,240)&&qr.GetPixel(0,0)==before,"Decoder does not mutate or dispose caller pixels");
                qr.RotateFlip(RotateFlipType.Rotate90FlipNone);Check(Has(BarcodeDecoder.Decode(qr),"QR",url),"Rotated QR can be read");
                using(Bitmap inverted=new Bitmap(qr.Width,qr.Height))
                {
                    using(Graphics g=Graphics.FromImage(inverted))using(ImageAttributes attributes=new ImageAttributes())
                    {
                        attributes.SetColorMatrix(new ColorMatrix(new float[][]{new float[]{-1,0,0,0,0},new float[]{0,-1,0,0,0},new float[]{0,0,-1,0,0},new float[]{0,0,0,1,0},new float[]{1,1,1,0,1}}));
                        g.DrawImage(qr,new Rectangle(Point.Empty,qr.Size),0,0,qr.Width,qr.Height,GraphicsUnit.Pixel,attributes);
                    }
                    Check(Has(BarcodeDecoder.Decode(inverted),"QR",url),"Inverted QR is decoded locally");
                }
                using(Bitmap transparent=new Bitmap(qr.Width,qr.Height,PixelFormat.Format32bppArgb))
                {
                    using(Graphics g=Graphics.FromImage(transparent))g.DrawImageUnscaled(qr,0,0);
                    transparent.MakeTransparent(Color.White);Check(Has(BarcodeDecoder.Decode(transparent),"QR",url),"Transparent PNG background is normalized to white");
                }
            }
            using(Bitmap qr=Code(BarcodeFormat.QR_CODE,unicode,260,260))Check(Has(BarcodeDecoder.Decode(qr),"QR",unicode),"Unicode and line breaks survive QR extraction");
            using(Bitmap code=Code(BarcodeFormat.CODE_128,"TRAZO-2026-123456",600,150))
            {
                Check(Has(BarcodeDecoder.Decode(code),"Code 128","TRAZO-2026-123456"),"Code 128 payload is read exactly");
                code.RotateFlip(RotateFlipType.Rotate90FlipNone);Check(Has(BarcodeDecoder.Decode(code),"Code 128","TRAZO-2026-123456"),"Vertical Code 128 supports automatic rotation");
            }
            using(Bitmap code=Code(BarcodeFormat.EAN_13,"5901234123457",500,170))Check(Has(BarcodeDecoder.Decode(code),"EAN-13","5901234123457"),"EAN-13 preserves all digits and checksum");
            using(Bitmap code=Code(BarcodeFormat.DATA_MATRIX,"TRAZO-DATAMATRIX-42",240,240))Check(Has(BarcodeDecoder.Decode(code),"Data Matrix","TRAZO-DATAMATRIX-42"),"Data Matrix is supported alongside QR and linear formats");
            using(Bitmap one=Code(BarcodeFormat.QR_CODE,url,240,240))using(Bitmap two=Code(BarcodeFormat.QR_CODE,unicode,240,240))using(Bitmap bars=Code(BarcodeFormat.CODE_128,"MULTI-12345",600,140))using(Bitmap canvas=new Bitmap(1000,520))
            {
                using(Graphics g=Graphics.FromImage(canvas)){g.Clear(Color.White);g.DrawImageUnscaled(one,20,20);g.DrawImageUnscaled(two,350,20);g.DrawImageUnscaled(bars,40,350);}
                BarcodeReadResult[] results=BarcodeDecoder.Decode(canvas);
                Check(Has(results,"QR",url)&&Has(results,"QR",unicode)&&Has(results,"Code 128","MULTI-12345"),"Mixed image returns both QR payloads and the linear barcode");
                Check(results.Length==3,"Multiple decoder passes do not duplicate results");
            }
            using(Bitmap blank=new Bitmap(350,200)){using(Graphics g=Graphics.FromImage(blank))g.Clear(Color.White);Check(BarcodeDecoder.Decode(blank).Length==0,"Blank image reports no code instead of guessing");}
            bool nullRejected=false;try{BarcodeDecoder.Decode(null);}catch(ArgumentNullException){nullRejected=true;}Check(nullRejected,"Null input is rejected explicitly");
            return count;
        }
    }
}
