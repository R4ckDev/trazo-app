using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Trazo
{
    internal static class UiSettingsTests
    {
        static int count;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool condition,string label){count++;if(!condition)throw new Exception(label);}
        static object Get(object target,string name){return target.GetType().GetField(name,Flags).GetValue(target);}
        static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Flags).Invoke(target,args);}
        static Control Find(Control root,string text){foreach(Control c in root.Controls){if(c.Text==text)return c;Control nested=Find(c,text);if(nested!=null)return nested;}return null;}
        static void Offscreen(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.Show();Application.DoEvents();}
        internal static int Run()
        {
            count=0;string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui_settings_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                AppSettings settings=new AppSettings();settings.HistoryLimit=2;settings.SaveDirectory=directory;
                CaptureHistoryStore store=new CaptureHistoryStore(directory);
                using(MainForm main=new MainForm(null,settings,directory,true))
                {
                    Offscreen(main);
                    Check(main.GetChildAtPoint(new Point(1,1))==null&&main.GetChildAtPoint(new Point(main.ClientSize.Width-2,1))==null&&main.GetChildAtPoint(new Point(1,main.ClientSize.Height-2))==null&&main.GetChildAtPoint(new Point(main.ClientSize.Width-2,main.ClientSize.Height-2))==null,"Dashboard corners stay free for native resizing instead of being covered by child controls");
                    Control brand=Find(main,"Trazo");Check(brand!=null&&brand.Parent.ClientRectangle.Contains(brand.Bounds),"Dashboard brand fits title bar");
                    Control caption=Find(main,"Capturas de pantalla · v"+Application.ProductVersion);Check(caption!=null&&caption.Parent.ClientRectangle.Contains(caption.Bounds),"Dashboard version and title fit header row");
                    Check(Find(main,"Capturar pantalla")!=null&&Find(main,"Configuración")!=null,"Dashboard exposes capture and settings");
                    FlowLayoutPanel gallery=(FlowLayoutPanel)Get(main,"gallery");Check(gallery.Controls.Count==0,"Empty dashboard starts without items");
                    using(Bitmap source=new Bitmap(90,60))using(OverlayForm overlay=new OverlayForm(source,new Rectangle(0,0,90,60)))
                    {
                        using(Graphics g=Graphics.FromImage(source))g.Clear(Color.DeepSkyBlue);
                        Call(main,"PrepareOverlay",overlay);
                        Check(overlay.DefaultSaveDirectory==directory&&overlay.DefaultImageFormat=="PNG","Dashboard passes output preferences to editor");
                        overlay.NotifyImageExported(source);overlay.NotifyImageExported(source);overlay.NotifyImageExported(source);
                        Check(store.Load().Count==2&&gallery.Controls.Count==2,"Dashboard applies configured retention after exports");
                    }
                    CaptureRecord first=store.Load()[0];Check(store.Delete(first),"Thumbnails do not lock history image files");main.RefreshHistory();Check(gallery.Controls.Count==1,"Dashboard refresh reflects removed history file");
                    using(CapturePreviewForm preview=new CapturePreviewForm(store.Load()[0]))
                    {
                        Check(preview.WindowState==FormWindowState.Normal,"Saved capture opens in a normal window instead of maximizing");
                        preview.CreateControl();preview.PerformLayout();
                        Call(Find(preview,"100 %"),"OnClick",EventArgs.Empty);
                        Check(((CaptureImageView)Get(preview,"picture")).Zoom==1,"Preview toolbar switches to actual image size");
                        Call(Find(preview,"Ajustar"),"OnClick",EventArgs.Empty);
                        preview.Close();Application.DoEvents();
                        Check(preview.IsDisposed,"Preview disposes cleanly after zoom and fit without a disposed-image layout failure");
                    }
                }
                using(Bitmap large=new Bitmap(2400,1600))using(Form host=new Form())using(CaptureImageView view=new CaptureImageView(large))
                {
                    host.ClientSize=new Size(600,400);view.Dock=DockStyle.Fill;host.Controls.Add(view);Offscreen(host);
                    double smallFit=view.Zoom;Check(smallFit<1&&!view.HorizontalScroll.Visible&&!view.VerticalScroll.Visible,"Fit shows the entire large image without scrollbars");
                    host.ClientSize=new Size(900,650);Application.DoEvents();Check(view.Zoom>smallFit,"Fit image grows when the viewer window grows");
                    view.SetZoom(1);Application.DoEvents();Check(view.Zoom==1&&view.HorizontalScroll.Visible&&view.VerticalScroll.Visible,"Actual size preserves full resolution and allows scrolling large captures");
                    view.AutoScrollPosition=new Point(300,200);Check(view.AutoScrollPosition.X<0&&view.AutoScrollPosition.Y<0,"Actual-size image can scroll to details beyond the viewport");
                    view.Fit();Application.DoEvents();Check(view.AutoScrollMinSize==Size.Empty&&!view.HorizontalScroll.Visible&&!view.VerticalScroll.Visible,"Fit resets scrolling after actual-size inspection");
                }
                using(Bitmap small=new Bitmap(90,60))using(CaptureImageView view=new CaptureImageView(small))
                {
                    view.Size=new Size(600,400);
                    Check(view.Zoom==1,"Fit keeps a small capture at actual pixel size instead of stretching it");
                    view.SetZoom(2);view.Fit();
                    Check(view.Zoom==1,"Fit restores actual size after manually enlarging a small capture");
                    view.Size=new Size(1000,700);
                    Check(view.Zoom==1,"Enlarging the preview window does not automatically enlarge a small capture");
                }
                Size minimumPreview=new Size(560,200);
                Check(CapturePreviewForm.CalculateInitialClientSize(new Size(800,600),new Size(1920,1000),minimumPreview,60,16)==new Size(832,692),"Preview uses actual capture dimensions plus image margins and toolbar when it fits");
                Check(CapturePreviewForm.CalculateInitialClientSize(new Size(2400,1200),new Size(1280,800),minimumPreview,60,16)==new Size(1280,716),"Wide capture is reduced proportionally to fit the available screen width");
                Check(CapturePreviewForm.CalculateInitialClientSize(new Size(1200,2400),new Size(1280,800),minimumPreview,60,16)==new Size(560,800),"Tall capture is reduced proportionally to fit screen height while keeping controls usable");
                Check(CapturePreviewForm.CalculateInitialClientSize(new Size(90,60),new Size(1280,800),minimumPreview,60,16)==new Size(560,200),"Small capture uses minimum usable window without enlarging image pixels");
                Check(CapturePreviewForm.CalculateInitialClientSize(new Size(800,600),new Size(400,240),minimumPreview,60,16)==new Size(400,240),"Preview minimum size is constrained by a smaller available screen");
                AppSettings disabled=settings.Clone();disabled.HistoryEnabled=false;
                using(MainForm main=new MainForm(null,disabled,directory,true))using(Bitmap source=new Bitmap(20,20))using(OverlayForm overlay=new OverlayForm(source,new Rectangle(0,0,20,20)))
                {Call(main,"PrepareOverlay",overlay);int before=store.Load().Count;overlay.NotifyImageExported(source);Check(store.Load().Count==before,"Disabled history does not store finished captures");}
                using(SettingsForm form=new SettingsForm(new AppSettings{SaveDirectory=directory},directory,false,null,true))
                {
                    Offscreen(form);
                    Control save=Find(form,"Guardar"),cancel=Find(form,"Cancelar"),close=Find(form,"×");
                    Check(save!=null&&save.Parent.ClientRectangle.Contains(save.Bounds),"Settings Save button stays visible");Check(cancel!=null&&cancel.Parent.ClientRectangle.Contains(cancel.Bounds),"Settings Cancel button stays visible");
                    Check(close!=null&&close.Parent.ClientRectangle.Contains(close.Bounds),"Settings title close button stays visible");
                    NumericUpDown limit=(NumericUpDown)Get(form,"historyLimit");Check(limit.Value==30&&limit.Minimum==1&&limit.Maximum==200,"Settings exposes default30 and maximum200");
                    limit.Value=200;((CheckBox)Get(form,"startup")).Checked=true;((ComboBox)Get(form,"closeBehavior")).SelectedIndex=(int)CloseBehavior.Tray;
                    Call(form,"SaveChanges");Check(form.ResultSettings.HistoryLimit==200&&form.ResultSettings.StartWithWindows&&form.ResultSettings.CloseBehavior==CloseBehavior.Tray,"Settings returns chosen history startup and closing behavior");
                }
                using(ClosePromptForm prompt=new ClosePromptForm())
                {Check(prompt.ResultBehavior==CloseBehavior.Ask&&!prompt.RememberChoice,"Close prompt does not silently remember a decision");Check(Find(prompt,"Dejar en la bandeja")!=null&&Find(prompt,"Cerrar Trazo")!=null&&Find(prompt,"Cancelar")!=null,"Close prompt offers tray exit and cancel");}
            }
            finally{foreach(string path in Directory.GetFiles(directory))File.Delete(path);Directory.Delete(directory);}
            return count;
        }
    }
}
