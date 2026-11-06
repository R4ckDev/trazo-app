using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Trazo
{
    internal sealed class FloatingCaptureForm:Form
    {
        private readonly Bitmap image;
        private readonly Rectangle initialWorkingArea;
        private readonly Panel toolbar;
        private readonly Button copy,close;
        private readonly TrackBar opacity;
        private readonly Label opacityValue;
        private readonly ToolTip tips=new ToolTip();
        private readonly Timer hoverTimer=new Timer{Interval=180};
        private bool disposed,initialBoundsApplied;

        [DllImport("user32.dll")]private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr first,IntPtr second);

        internal FloatingCaptureForm(Bitmap source,Rectangle workingArea)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(workingArea.Width<1||workingArea.Height<1)throw new ArgumentOutOfRangeException("workingArea");
            image=(Bitmap)source.Clone();initialWorkingArea=workingArea;
            Text="Trazo · Captura flotante";AccessibleName=Text;
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
            TopMost=true;ShowInTaskbar=false;MaximizeBox=false;MinimizeBox=false;KeyPreview=true;
            Font=new Font("Segoe UI",9);BackColor=Color.FromArgb(227,234,242);DoubleBuffered=true;
            Padding=new Padding(ResizeBorderWidth);Rectangle initial=CalculateInitialBounds(image.Size,workingArea,ResizeBorderWidth);
            MinimumSize=new Size(Math.Min(ScaleValue(200),initial.Width),Math.Min(ScaleValue(120),initial.Height));Bounds=initial;Cursor=Cursors.SizeAll;

            toolbar=new Panel{BackColor=Color.White,Visible=false,TabStop=false,Cursor=Cursors.Default};
            copy=ActionButton("Copiar","Copiar captura (Ctrl+C)");copy.Click+=delegate{CopyToClipboard();};
            close=ActionButton("×","Cerrar captura flotante (Esc)");close.Click+=delegate{Close();};close.ForeColor=Color.FromArgb(203,58,68);
            opacityValue=new Label{Text="100 %",TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(65,81,99),AccessibleName="Opacidad de la captura"};
            opacity=new TrackBar{Minimum=30,Maximum=100,Value=100,TickStyle=TickStyle.None,AutoSize=false,SmallChange=5,LargeChange=10,AccessibleName="Opacidad de la captura, de 30 a 100 por ciento"};
            opacity.ValueChanged+=delegate{SetOpacityPercent(opacity.Value);};
            toolbar.Controls.Add(copy);toolbar.Controls.Add(opacity);toolbar.Controls.Add(opacityValue);toolbar.Controls.Add(close);Controls.Add(toolbar);
            tips.SetToolTip(copy,"Copiar la imagen original");tips.SetToolTip(close,"Cerrar esta referencia; la captura se conserva en el historial");tips.SetToolTip(opacity,"Ajustar la opacidad");
            tips.SetToolTip(this,"Arrastra la imagen para moverla. Usa los bordes y las esquinas para cambiar su tamaño.");
            hoverTimer.Tick+=delegate{UpdateToolbarVisibility();};
            LayoutToolbar();
        }

        private int ResizeBorderWidth{get{return Math.Max(8,(int)Math.Round(8*DeviceDpi/96.0));}}
        private int ScaleValue(int value){return Math.Max(1,(int)Math.Round(value*DeviceDpi/96.0));}
        private Button ActionButton(string text,string name)
        {
            Button button=new Button{Text=text,AccessibleName=name,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(50,70,90),Cursor=Cursors.Hand,TabStop=true};
            button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(229,242,253);return button;
        }
        internal static Rectangle CalculateInitialBounds(Size imageSize,Rectangle workingArea,int border)
        {
            if(imageSize.Width<1||imageSize.Height<1)throw new ArgumentOutOfRangeException("imageSize");
            if(workingArea.Width<1||workingArea.Height<1)throw new ArgumentOutOfRangeException("workingArea");
            if(border<0)throw new ArgumentOutOfRangeException("border");
            int gap=Math.Min(12,Math.Min(workingArea.Width,workingArea.Height)/8);
            Rectangle available=Rectangle.Inflate(workingArea,-gap,-gap);
            double scale=Math.Min(1,Math.Min(Math.Max(1,available.Width-border*2)/(double)imageSize.Width,Math.Max(1,available.Height-border*2)/(double)imageSize.Height));
            int width=Math.Min(available.Width,Math.Max(Math.Max(200,border*25),(int)Math.Ceiling(imageSize.Width*scale)+border*2));
            int height=Math.Min(available.Height,Math.Max(Math.Max(120,border*15),(int)Math.Ceiling(imageSize.Height*scale)+border*2));
            return new Rectangle(workingArea.Left+(workingArea.Width-width)/2,workingArea.Top+(workingArea.Height-height)/2,width,height);
        }
        internal Bitmap CreateImageSnapshot()
        {
            if(disposed)throw new ObjectDisposedException("FloatingCaptureForm");
            return (Bitmap)image.Clone();
        }
        internal void SetOpacityPercent(int percent)
        {
            int value=Math.Max(30,Math.Min(100,percent));
            Opacity=value/100.0;opacityValue.Text=value+" %";
            if(opacity.Value!=value)opacity.Value=value;
        }
        private void CopyToClipboard()
        {
            try{using(Bitmap snapshot=CreateImageSnapshot())Clipboard.SetDataObject(snapshot,true,8,80);}
            catch(Exception error){MessageBox.Show(this,"No se pudo copiar la captura.\n"+error.Message,"Trazo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private void LayoutToolbar()
        {
            if(toolbar==null)return;
            int inset=ResizeBorderWidth+ScaleValue(4),height=ScaleValue(38),width=Math.Max(1,Math.Min(ScaleValue(350),ClientSize.Width-inset*2));
            toolbar.Bounds=new Rectangle(Math.Max(ResizeBorderWidth,ClientSize.Width-inset-width),inset,width,height);
            int copyWidth=ScaleValue(56),closeWidth=ScaleValue(32),labelWidth=ScaleValue(46),padding=ScaleValue(3);
            copy.Bounds=new Rectangle(padding,padding,copyWidth,Math.Max(1,height-padding*2));
            close.Bounds=new Rectangle(Math.Max(0,width-closeWidth-padding),padding,closeWidth,Math.Max(1,height-padding*2));
            opacityValue.Bounds=new Rectangle(Math.Max(0,close.Left-labelWidth),padding,labelWidth,Math.Max(1,height-padding*2));
            opacity.Bounds=new Rectangle(copy.Right+padding,ScaleValue(4),Math.Max(1,opacityValue.Left-copy.Right-padding),Math.Max(1,height-ScaleValue(8)));
            toolbar.BringToFront();
        }
        private void UpdateToolbarVisibility()
        {
            if(disposed||!IsHandleCreated)return;
            if(!Visible){toolbar.Hide();return;}
            bool show=ClientRectangle.Contains(PointToClient(System.Windows.Forms.Cursor.Position))||ContainsFocus;
            if(toolbar.Visible!=show)toolbar.Visible=show;
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);Padding=new Padding(ResizeBorderWidth);
            if(!initialBoundsApplied){initialBoundsApplied=true;Rectangle initial=CalculateInitialBounds(image.Size,initialWorkingArea,ResizeBorderWidth);MinimumSize=new Size(Math.Min(ScaleValue(200),initial.Width),Math.Min(ScaleValue(120),initial.Height));Bounds=initial;}
            LayoutToolbar();hoverTimer.Start();UpdateToolbarVisibility();
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);LayoutToolbar();Invalidate();}
        protected override void OnDpiChanged(DpiChangedEventArgs e){base.OnDpiChanged(e);Padding=new Padding(ResizeBorderWidth);Rectangle work=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(ScaleValue(200),work.Width),Math.Min(ScaleValue(120),work.Height));LayoutToolbar();Invalidate();}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);if(toolbar!=null)toolbar.Show();}
        protected override void OnActivated(EventArgs e){base.OnActivated(e);if(toolbar!=null)toolbar.Show();}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0x00A1,new IntPtr(2),IntPtr.Zero);}
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keys)
        {
            if(keys==Keys.Escape){Close();return true;}
            if(keys==(Keys.Control|Keys.C)){CopyToClipboard();return true;}
            return base.ProcessCmdKey(ref msg,keys);
        }
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x0084&&WindowState==FormWindowState.Normal)
            {
                long value=message.LParam.ToInt64();Point point=PointToClient(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));
                int border=ResizeBorderWidth;
                bool left=point.X>=0&&point.X<border,right=point.X<ClientSize.Width&&point.X>=ClientSize.Width-border,top=point.Y>=0&&point.Y<border,bottom=point.Y<ClientSize.Height&&point.Y>=ClientSize.Height-border;
                int hit=left&&top?13:right&&top?14:left&&bottom?16:right&&bottom?17:left?10:right?11:top?12:bottom?15:0;
                if(hit!=0){message.Result=new IntPtr(hit);return;}
            }
            base.WndProc(ref message);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(image==null||disposed)return;
            Rectangle canvas=DisplayRectangle;
            if(canvas.Width>0&&canvas.Height>0)
            {
                e.Graphics.FillRectangle(Brushes.White,canvas);
                double scale=Math.Min(1,Math.Min(canvas.Width/(double)image.Width,canvas.Height/(double)image.Height));
                int width=Math.Max(1,(int)Math.Round(image.Width*scale)),height=Math.Max(1,(int)Math.Round(image.Height*scale));
                Rectangle target=new Rectangle(canvas.Left+(canvas.Width-width)/2,canvas.Top+(canvas.Height-height)/2,width,height);
                if(target.Size==image.Size)e.Graphics.DrawImageUnscaled(image,target.Location);
                else{e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;e.Graphics.DrawImage(image,target,new Rectangle(Point.Empty,image.Size),GraphicsUnit.Pixel);}
            }
            using(Pen outline=new Pen(Color.FromArgb(113,151,188)))e.Graphics.DrawRectangle(outline,0,0,Math.Max(0,ClientSize.Width-1),Math.Max(0,ClientSize.Height-1));
            using(Pen grip=new Pen(Color.FromArgb(94,128,163))){for(int i=0;i<3;i++){int offset=3+i*3;e.Graphics.DrawLine(grip,ClientSize.Width-offset-1,ClientSize.Height-2,ClientSize.Width-2,ClientSize.Height-offset-1);}}
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!disposed){hoverTimer.Stop();hoverTimer.Dispose();tips.Dispose();base.Dispose(true);disposed=true;image.Dispose();return;}
            base.Dispose(disposing);
        }
    }
}
