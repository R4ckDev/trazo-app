using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Trazo
{
    internal static class FloatingCaptureTests
    {
        private static int count;
        private static void Check(bool value,string message){count++;if(!value)throw new Exception(message);}
        internal static int Run()
        {
            count=0;
            Rectangle monitor=new Rectangle(-1600,40,1600,860);
            Rectangle normal=FloatingCaptureForm.CalculateInitialBounds(new Size(640,480),monitor,8);
            Check(normal.Size==new Size(656,496),"Floating capture starts at original image dimensions plus border");
            Check(monitor.Contains(normal),"Floating capture starts inside the supplied monitor, including negative origins");
            Rectangle large=FloatingCaptureForm.CalculateInitialBounds(new Size(5000,3000),monitor,8);
            Check(monitor.Contains(large)&&large.Width<1600&&large.Height<860,"Oversized floating capture fits working area without maximization");
            Rectangle tiny=FloatingCaptureForm.CalculateInitialBounds(new Size(20,15),new Rectangle(0,0,400,240),8);
            Check(tiny.Size==new Size(200,120),"Tiny floating captures keep controls and resizing reachable");
            Rectangle limited=FloatingCaptureForm.CalculateInitialBounds(new Size(800,600),new Rectangle(0,0,160,100),8);
            Check(new Rectangle(0,0,160,100).Contains(limited),"Small working areas bound floating windows even below preferred minimum");
            using(Bitmap smallSource=new Bitmap(200,100))using(FloatingCaptureForm smallWindow=new FloatingCaptureForm(smallSource,new Rectangle(0,0,160,100)))Check(new Rectangle(0,0,160,100).Contains(smallWindow.Bounds),"Actual floating window minimum cannot push it beyond a small working area");
            Bitmap source=new Bitmap(240,160);source.SetPixel(100,80,Color.RoyalBlue);
            FloatingCaptureForm floating=new FloatingCaptureForm(source,monitor);
            try
            {
                Check(!floating.Visible&&!floating.IsHandleCreated,"Constructing a floating capture does not open a visible test window");
                Check(floating.TopMost&&!floating.ShowInTaskbar&&floating.Owner==null,"Floating capture is independently topmost without being owned by the dashboard");
                Check(floating.FormBorderStyle==FormBorderStyle.None,"Floating capture has no permanent title bar");
                source.SetPixel(100,80,Color.Red);source.Dispose();source=null;
                using(Bitmap snapshot=floating.CreateImageSnapshot())
                {
                    Check(snapshot.Size==new Size(240,160)&&snapshot.GetPixel(100,80).ToArgb()==Color.RoyalBlue.ToArgb(),"Floating capture owns independent original pixels after source mutation and disposal");
                    snapshot.SetPixel(100,80,Color.Green);
                }
                using(Bitmap snapshot=floating.CreateImageSnapshot())Check(snapshot.GetPixel(100,80).ToArgb()==Color.RoyalBlue.ToArgb(),"Copied snapshots cannot mutate the pinned image");
                floating.SetOpacityPercent(55);Check(Math.Abs(floating.Opacity-.55)<.001,"Transparency control updates window opacity");
                floating.SetOpacityPercent(0);Check(Math.Abs(floating.Opacity-.30)<.001,"Transparency is bounded so controls remain visible");
                floating.SetOpacityPercent(140);Check(floating.Opacity==1,"Transparency returns to fully opaque");
                using(Bitmap snapshot=floating.CreateImageSnapshot())Check(snapshot.GetPixel(100,80).ToArgb()==Color.RoyalBlue.ToArgb(),"Transparency does not modify original image pixels");
                floating.ClientSize=new Size(400,300);
                using(Bitmap render=new Bitmap(400,300))using(Graphics graphics=Graphics.FromImage(render))
                {
                    typeof(FloatingCaptureForm).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(floating,new object[]{new PaintEventArgs(graphics,new Rectangle(0,0,400,300))});
                    Check(render.GetPixel(200,150).A==255,"Floating image paints without showing or activating a window");
                    Check(render.GetPixel(180,150).ToArgb()==Color.RoyalBlue.ToArgb(),"Floating rendering keeps the original image at natural size when the window has extra space");
                }
                Check(floating.GetChildAtPoint(new Point(1,1))==null&&floating.GetChildAtPoint(new Point(398,298))==null,"Floating corners remain free of child controls for resizing");
            }
            finally{floating.Dispose();if(source!=null)source.Dispose();}
            Check(floating.IsDisposed,"Closing a floating capture disposes only its own window");
            bool disposed=false;try{using(Bitmap ignored=floating.CreateImageSnapshot()){}}catch(ObjectDisposedException){disposed=true;}
            Check(disposed,"Disposed floating capture no longer exposes image snapshots");
            return count;
        }
    }
}
