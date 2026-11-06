using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ZXing;

namespace Trazo
{
    internal static class BarcodeIntegrationTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static int count;
        private static void Check(bool value,string label){count++;if(!value)throw new Exception(label);}
        private static object Call(object target,string name,params object[] values){return target.GetType().GetMethod(name,Flags).Invoke(target,values);}
        private static Control Find(Control root,string text){foreach(Control child in root.Controls){if(child.Text==text)return child;Control found=Find(child,text);if(found!=null)return found;}return null;}
        private static MouseEventArgs Mouse(int x,int y){return new MouseEventArgs(MouseButtons.Left,1,x,y,0);}
        internal static int Run()
        {
            count=0;string payload="TRAZO-CROP-QR-42";
            using(Bitmap qr=BarcodeDecoderTests.Code(BarcodeFormat.QR_CODE,payload,240,240))using(Bitmap source=new Bitmap(600,420))
            {
                using(Graphics g=Graphics.FromImage(source)){g.Clear(Color.White);g.DrawImageUnscaled(qr,60,60);}
                using(OverlayForm overlay=new OverlayForm(source,new Rectangle(0,0,600,420)))
                {
                    int exports=0;overlay.ImageExported+=delegate{exports++;};
                    Call(overlay,"OnMouseDown",Mouse(40,40));Call(overlay,"OnMouseMove",Mouse(340,340));Call(overlay,"OnMouseUp",Mouse(340,340));
                    Control read=Find(overlay,"Leer QR y barras");Check(read!=null,"Regular capture exposes a labeled QR and barcode action");
                    using(Bitmap crop=overlay.RenderSelection())
                    {
                        BarcodeReadResult[] results=BarcodeDecoder.Decode(crop);Check(crop.Size==new Size(300,300)&&results.Length==1&&results[0].Text==payload,"Reader uses the selected image crop at original resolution");
                    }
                    Call(read,"OnClick",EventArgs.Empty);Check(exports==0,"Reading selected codes does not add a screenshot to history");
                    Check(Find(overlay,"Captura larga")==null,"Removed scrolling capture action is not exposed");
                }
                using(OverlayForm readerMode=new OverlayForm(source,new Rectangle(0,0,600,420),new CaptureWindow[0],true))
                {
                    Call(readerMode,"ReadCodes");Check(readerMode.CodeImage==null&&!readerMode.IsDisposed,"Reader selection does not scan until an area is chosen");
                }
                using(CapturePreviewForm preview=new CapturePreviewForm(qr))
                {
                    Check(Find(preview,"Leer QR y barras")!=null,"Saved-image viewer exposes code reading");
                    using(Bitmap snapshot=preview.CreateImageSnapshot())Check(BarcodeDecoder.Decode(snapshot)[0].Text==payload,"Saved-image reader preserves the source code pixels");
                }
            }
            using(MainForm main=new MainForm(null,new AppSettings(),System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"unused-qr-history"),true))
            {
                Control read=Find(main,"Leer QR y barras");Check(read!=null&&Find(main,"Captura larga")==null,"Dashboard offers code reading instead of scrolling capture");
                main.Size=main.MinimumSize;main.PerformLayout();read.Parent.PerformLayout();Check(read.Parent.ClientRectangle.Contains(read.Bounds),"Reader action fits the smallest dashboard width");
            }
            return count;
        }
    }
}
