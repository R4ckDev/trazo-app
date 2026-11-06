using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

namespace Trazo
{
    internal static class CaptureDimensionsTests
    {
        private static int count;
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool value,string label){count++;if(!value)throw new Exception(label);}
        private static object Field(object target,string name){return target.GetType().GetField(name,Flags).GetValue(target);}
        private static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,Flags).Invoke(target,args);}
        private static MouseEventArgs Mouse(Point point){return new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0);}
        internal static int Run()
        {
            count=0;Size bounds=new Size(800,600);
            CaptureSelectionConstraint exact=new CaptureSelectionConstraint(CaptureSizeMode.Exact,320,180);
            Check(exact.FromDrag(new Point(50,40),new Point(100,100),bounds)==new Rectangle(50,40,320,180),"Exact mode preserves requested pixel dimensions");
            Check(exact.FromDrag(new Point(790,590),new Point(800,600),bounds)==new Rectangle(480,420,320,180),"Exact capture clamps position at lower-right without scaling");
            Check(exact.FromDrag(new Point(200,100),new Point(0,0),bounds)==new Rectangle(0,0,320,180),"Exact reverse drag clamps without changing dimensions");
            Check(!new CaptureSelectionConstraint(CaptureSizeMode.Exact,900,600).Fits(bounds),"Oversized exact capture is rejected");
            Check(exact.Resize(new Rectangle(10,10,320,180),4,new Point(700,500),bounds)==new Rectangle(10,10,320,180),"Exact dimensions stay locked when resizing");
            CaptureSelectionConstraint ratio=new CaptureSelectionConstraint(CaptureSizeMode.Ratio,1920,1080);
            Check(ratio.Width==16&&ratio.Height==9,"Ratio is reduced to exact integer units");
            Point[] directions={new Point(790,590),new Point(5,590),new Point(5,5),new Point(790,5)};
            foreach(Point end in directions)
            {
                Rectangle r=ratio.FromDrag(new Point(400,300),end,bounds);
                Check(r.Width*9==r.Height*16&&r.Width>0,"Drag preserves ratio in all directions");
                Check(new Rectangle(Point.Empty,bounds).Contains(r),"Ratio drag stays in image bounds");
            }
            Rectangle current=new Rectangle(160,180,320,180);
            Point[] moves={new Point(0,0),new Point(320,0),new Point(800,0),new Point(800,270),new Point(800,600),new Point(320,600),new Point(0,600),new Point(0,270)};
            for(int handle=0;handle<8;handle++)
            {
                Rectangle resized=ratio.Resize(current,handle,moves[handle],bounds);
                Check(resized.Width*9==resized.Height*16&&resized.Width>=16,"Every selection handle preserves ratio");
                Check(new Rectangle(Point.Empty,bounds).Contains(resized),"Every constrained resize stays inside image");
                if(handle==0)Check(resized.Right==current.Right&&resized.Bottom==current.Bottom,"Upper-left resize anchors opposite corner");
                if(handle==4)Check(resized.Location==current.Location,"Lower-right resize anchors opposite corner");
                if(handle==3)Check(resized.Left==current.Left,"Right edge anchors opposite edge");
                if(handle==5)Check(resized.Top==current.Top,"Bottom edge anchors opposite edge");
            }
            Rectangle fitted=ratio.Apply(new Rectangle(0,0,800,600),bounds);
            Check(fitted.Width==800&&fitted.Height==450&&new Rectangle(Point.Empty,bounds).Contains(fitted),"Applying ratio fits largest requested bounded size");
            using(Bitmap fixture=new Bitmap(800,600,PixelFormat.Format32bppArgb))
            {
                using(Graphics g=Graphics.FromImage(fixture)){g.Clear(Color.White);g.FillRectangle(Brushes.RoyalBlue,100,100,30,30);}
                using(OverlayForm overlay=new OverlayForm(fixture,new Rectangle(-800,-100,800,600)))
                {
                    overlay.ApplyCaptureConstraint(ratio,new Rectangle(0,0,800,600));
                    Call(overlay,"OnMouseDown",Mouse(new Point(100,100)));Call(overlay,"OnMouseMove",Mouse(new Point(420,290)));Call(overlay,"OnMouseUp",Mouse(new Point(420,290)));
                    Rectangle selected=(Rectangle)Field(overlay,"selection");Check(selected.Size==new Size(336,189),"Real overlay drag uses selected aspect ratio");
                    using(Bitmap output=overlay.RenderSelection()){Check(output.Size==selected.Size,"Ratio export keeps selected dimensions");Check(output.GetPixel(5,5).ToArgb()==Color.RoyalBlue.ToArgb(),"Ratio export crops source pixels without resampling");}
                    overlay.ApplyCaptureConstraint(exact,selected);selected=(Rectangle)Field(overlay,"selection");
                    Check(selected.Size==new Size(320,180),"Exact selection applies immediately");
                    Point start=new Point(selected.Left+50,selected.Top+50);
                    Call(overlay,"OnMouseDown",Mouse(start));Call(overlay,"OnMouseMove",Mouse(new Point(790,590)));Call(overlay,"OnMouseUp",Mouse(new Point(790,590)));
                    selected=(Rectangle)Field(overlay,"selection");Check(selected.Size==new Size(320,180)&&selected.Right<=800&&selected.Bottom<=600,"Moving exact capture keeps size at screen edges");
                    bool rejected=false;try{overlay.ApplyCaptureConstraint(new CaptureSelectionConstraint(CaptureSizeMode.Exact,900,100),selected);}catch(ArgumentException){rejected=true;}
                    Check(rejected&&(Rectangle)Field(overlay,"selection")==selected,"Rejected size does not change selected pixels");
                    Call(overlay,"FullScreen");Check(((CaptureSelectionConstraint)Field(overlay,"captureConstraint")).Mode==CaptureSizeMode.Free,"Explicit full-screen returns to unconstrained selection");
                }
            }
            using(CaptureSizeForm form=new CaptureSizeForm(CaptureSelectionConstraint.Free(),new Size(640,480),bounds))
            {
                ComboBox mode=(ComboBox)Field(form,"mode"),preset=(ComboBox)Field(form,"preset"),ratios=(ComboBox)Field(form,"ratio");
                mode.SelectedIndex=1;preset.SelectedIndex=1;Call(form,"Apply");Check(form.DialogResult==DialogResult.None,"Dialog stays open for a preset that exceeds screen bounds");
                preset.SelectedIndex=5;Call(form,"Apply");Check(form.Result.Mode==CaptureSizeMode.Exact&&form.Result.Width==800&&form.Result.Height==600,"Dialog applies exact size preset");
            }
            using(CaptureSizeForm form=new CaptureSizeForm(CaptureSelectionConstraint.Free(),new Size(640,480),bounds))
            {
                ((ComboBox)Field(form,"mode")).SelectedIndex=2;((ComboBox)Field(form,"ratio")).SelectedIndex=3;Call(form,"Apply");
                Check(form.Result.Mode==CaptureSizeMode.Ratio&&form.Result.Width==9&&form.Result.Height==16,"Dialog applies vertical ratio preset");
            }
            return count;
        }
    }
}
