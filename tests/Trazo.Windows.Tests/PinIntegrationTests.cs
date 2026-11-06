using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Trazo
{
    internal static class PinIntegrationTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static int count;
        private static void Check(bool value,string label){count++;if(!value)throw new Exception(label);}
        private static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Flags).Invoke(target,args);}
        private static void Set(object target,string name,object value){target.GetType().GetField(name,Flags).SetValue(target,value);}
        private static Control Find(Control root,string text){foreach(Control child in root.Controls){if(child.Text==text)return child;Control found=Find(child,text);if(found!=null)return found;}return null;}
        internal static int Run()
        {
            count=0;string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pin_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                CaptureHistoryStore store=new CaptureHistoryStore(folder);
                using(Bitmap source=new Bitmap(100,80))
                {
                    using(Graphics g=Graphics.FromImage(source))g.Clear(Color.White);
                    Bitmap delivered=null;int exports=0;
                    using(OverlayForm overlay=new OverlayForm(source,new Rectangle(0,0,100,80)))
                    {
                        Set(overlay,"selection",new Rectangle(10,10,60,40));Set(overlay,"ready",true);
                        Annotation rect=new Annotation{Tool=DrawTool.Rect,Start=new Point(20,20),End=new Point(50,40),Color=Color.Red,Width=3};Call(overlay,"AddAnnotation",rect);
                        Set(overlay,"effects",new ExportOptions{Border=true});
                        overlay.ImageExported+=delegate(Bitmap image){exports++;store.Add(image,30);delivered=(Bitmap)image.Clone();};
                        Call(overlay,"PinImage");Call(overlay,"PinImage");
                        Check(exports==1&&delivered!=null,"Pin finishes capture and notifies history exactly once");
                    }
                    using(delivered)
                    {
                        Check(delivered.Size==new Size(84,64),"Pinned capture includes final crop and export effects");
                        Color mark=delivered.GetPixel(22,27);Check(mark.R>200&&mark.G<150,"Pinned pixels include flattened annotations");
                        Check(delivered.GetPixel(0,0).ToArgb()==Color.White.ToArgb(),"Pinned export excludes selection handles and overlay controls");
                    }
                }
                CaptureRecord record=store.Load()[0];Bitmap snapshot;
                using(CapturePreviewForm preview=new CapturePreviewForm(record))
                {
                    Check(Find(preview,"Fijar en pantalla")!=null,"History viewer exposes pin action");snapshot=preview.CreateImageSnapshot();
                    Call(preview,"RequestPin");Check(preview.PinRequested&&preview.PinWorkingArea.Width>0,"History pin requests include the viewer monitor");
                }
                using(snapshot)
                {
                    Check(snapshot.Width==84,"Pin snapshot survives closing the history viewer");
                    AppSettings settings=new AppSettings{SaveDirectory=folder};
                    FloatingCaptureForm first,second;
                    using(MainForm main=new MainForm(null,settings,folder,true))
                    {
                        first=(FloatingCaptureForm)Call(main,"CreateFloatingCapture",snapshot,new Rectangle(0,0,1000,800));
                        second=(FloatingCaptureForm)Call(main,"CreateFloatingCapture",snapshot,new Rectangle(-1000,0,1000,800));
                        Check(!first.Visible&&!second.Visible&&first.Owner==null&&second.Owner==null,"Creating references does not show test windows or tie their lifetime to a modal");
                        first.Dispose();Check(!second.IsDisposed,"Closing one floating capture preserves other references");
                        Check(store.Load().Count==1&&File.Exists(record.FullPath),"Closing a reference preserves the history image");
                    }
                    Check(second.IsDisposed,"Exiting dashboard disposes its remaining floating captures");
                    Check(store.Load().Count==1,"Exiting references does not remove capture history");
                }
            }
            finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
            return count;
        }
    }
}
