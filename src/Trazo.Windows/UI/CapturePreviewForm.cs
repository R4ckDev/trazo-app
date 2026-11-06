using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace Trazo
{
    internal sealed class CapturePreviewForm:Form
    {
        private readonly Bitmap image;
        private readonly CaptureImageView picture;
        private readonly FlowLayoutPanel toolbar;
        internal bool PinRequested {get;private set;}
        internal Rectangle PinWorkingArea {get;private set;}
        internal CapturePreviewForm(CaptureRecord record) : this(LoadImage(record),record.CreatedUtc,true) { }
        internal CapturePreviewForm(Bitmap image) : this(image,DateTime.UtcNow,false) { }
        private static Bitmap LoadImage(CaptureRecord record){using(Image source=Image.FromFile(record.FullPath))return new Bitmap(source);}
        private CapturePreviewForm(Bitmap sourceImage,DateTime createdUtc,bool ownsImage)
        {
            image=ownsImage?sourceImage:new Bitmap(sourceImage);
            Text="Trazo · "+createdUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm")+" · "+image.Width+" × "+image.Height;
            StartPosition=FormStartPosition.Manual;ClientSize=new Size(560,300);MinimumSize=new Size(560,200);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;BackColor=Color.FromArgb(235,239,244);Font=new Font("Segoe UI",10);KeyPreview=true;
            toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=76,Padding=new Padding(12,10,12,8),BackColor=Color.White,WrapContents=false,AutoScroll=true};
            Button edit=ActionButton("Editar");edit.Click+=delegate{DialogResult=DialogResult.Yes;Close();};toolbar.Controls.Add(edit);
            Button read=ActionButton("Leer QR y barras");read.Width=154;read.Click+=delegate{using(BarcodeResultsForm reader=new BarcodeResultsForm(image))reader.ShowDialog(this);};toolbar.Controls.Add(read);
            Button pin=ActionButton("Fijar en pantalla");pin.Width=146;pin.AccessibleName="Fijar en pantalla (Ctrl+Mayús+P)";pin.Click+=delegate{RequestPin();};toolbar.Controls.Add(pin);
            Button copy=ActionButton("Copiar");copy.Click+=delegate{try{Clipboard.SetDataObject(image,true,8,80);}catch(Exception error){MessageBox.Show(this,error.Message,"Trazo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};toolbar.Controls.Add(copy);
            Button save=ActionButton("Guardar como…");save.Width=145;save.Click+=delegate{using(SaveFileDialog dialog=new SaveFileDialog{Filter="Imagen PNG|*.png|Imagen JPEG|*.jpg|Mapa de bits|*.bmp",FileName="Trazo_"+createdUtc.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss")+".png",AddExtension=true,DefaultExt="png",OverwritePrompt=true}){if(dialog.ShowDialog(this)==DialogResult.OK){try{WriteImage(image,dialog.FileName);}catch(Exception error){MessageBox.Show(this,error.Message,"Trazo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}}};toolbar.Controls.Add(save);
            picture=new CaptureImageView(image){Dock=DockStyle.Fill};
            Button fit=ActionButton("Ajustar");fit.Click+=delegate{picture.Fit();};toolbar.Controls.Add(fit);
            Button actual=ActionButton("100 %");actual.Width=76;actual.Click+=delegate{picture.SetZoom(1);};toolbar.Controls.Add(actual);
            Button smaller=ActionButton("−");smaller.Width=42;smaller.AccessibleName="Alejar imagen";smaller.Click+=delegate{picture.SetZoom(picture.Zoom/1.25);};toolbar.Controls.Add(smaller);
            Button larger=ActionButton("+");larger.Width=42;larger.AccessibleName="Acercar imagen";larger.Click+=delegate{picture.SetZoom(picture.Zoom*1.25);};toolbar.Controls.Add(larger);
            Label zoom=new Label{AutoSize=true,Margin=new Padding(8,8,0,0),ForeColor=Color.FromArgb(75,90,105)};toolbar.Controls.Add(zoom);
            picture.ViewChanged+=delegate{zoom.Text=Math.Round(picture.Zoom*100)+" %";};
            Controls.Add(picture);Controls.Add(toolbar);
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}else if(e.Control&&e.Shift&&e.KeyCode==Keys.R){using(BarcodeResultsForm reader=new BarcodeResultsForm(image))reader.ShowDialog(this);e.SuppressKeyPress=true;}else if(e.Control&&e.Shift&&e.KeyCode==Keys.P){RequestPin();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.D0){picture.Fit();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.D1){picture.SetZoom(1);e.SuppressKeyPress=true;}};
        }
        private void RequestPin(){PinRequested=true;PinWorkingArea=Screen.FromControl(this).WorkingArea;DialogResult=DialogResult.OK;Close();}
        internal Bitmap CreateImageSnapshot(){return new Bitmap(image);}
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle work=(Owner==null?Screen.FromPoint(Cursor.Position):Screen.FromControl(Owner)).WorkingArea;
            int gap=Math.Max(8,(int)Math.Round(12*DeviceDpi/96.0));
            Rectangle available=work.Width>gap*2&&work.Height>gap*2?Rectangle.Inflate(work,-gap,-gap):work;
            Size frame=new Size(Width-ClientSize.Width,Height-ClientSize.Height);
            MinimumSize=new Size(Math.Min(MinimumSize.Width,available.Width),Math.Min(MinimumSize.Height,available.Height));
            Size availableClient=new Size(Math.Max(1,available.Width-frame.Width),Math.Max(1,available.Height-frame.Height));
            Size minimumClient=new Size(Math.Max(1,MinimumSize.Width-frame.Width),Math.Max(1,MinimumSize.Height-frame.Height));
            ClientSize=CalculateInitialClientSize(image.Size,availableClient,minimumClient,toolbar.Height,CaptureImageView.ImageMargin);
            Rectangle center=Owner==null?work:Owner.Bounds;
            Location=new Point(Math.Max(available.Left,Math.Min(available.Right-Width,center.Left+(center.Width-Width)/2)),Math.Max(available.Top,Math.Min(available.Bottom-Height,center.Top+(center.Height-Height)/2)));
        }
        internal static Size CalculateInitialClientSize(Size imageSize,Size availableClient,Size minimumClient,int toolbarHeight,int imageMargin)
        {
            int width=Math.Max(1,availableClient.Width),height=Math.Max(1,availableClient.Height);
            double scale=Math.Min(1,Math.Min(Math.Max(1,width-imageMargin*2)/(double)imageSize.Width,Math.Max(1,height-toolbarHeight-imageMargin*2)/(double)imageSize.Height));
            int desiredWidth=(int)Math.Ceiling(imageSize.Width*scale)+imageMargin*2;
            int desiredHeight=(int)Math.Ceiling(imageSize.Height*scale)+imageMargin*2+toolbarHeight;
            return new Size(Math.Min(width,Math.Max(minimumClient.Width,desiredWidth)),Math.Min(height,Math.Max(minimumClient.Height,desiredHeight)));
        }
        private static Button ActionButton(string text){return new Button{Text=text,Size=new Size(102,34),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(45,60,75),Margin=new Padding(0,0,8,0)};}
        internal static void WriteImage(Bitmap image,string path)
        {
            string extension=Path.GetExtension(path).ToLowerInvariant();ImageFormat format=extension==".jpg"||extension==".jpeg"?ImageFormat.Jpeg:extension==".bmp"?ImageFormat.Bmp:ImageFormat.Png;
            if(format==ImageFormat.Png){image.Save(path,format);return;}
            using(Bitmap flat=new Bitmap(image.Width,image.Height)){using(Graphics g=Graphics.FromImage(flat)){g.Clear(Color.White);g.DrawImageUnscaled(image,0,0);}flat.Save(path,format);}
        }
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)image.Dispose();}
    }

    internal sealed class CaptureImageView:ScrollableControl
    {
        internal const int ImageMargin=16;
        private readonly Bitmap image;
        private bool fit=true;
        private double zoom=1;
        internal event Action ViewChanged;
        internal double Zoom {get{return fit?Math.Max(0.001,Math.Min(1,Math.Min(Math.Max(1,ClientSize.Width-ImageMargin*2)/(double)image.Width,Math.Max(1,ClientSize.Height-ImageMargin*2)/(double)image.Height))):zoom;}}
        internal CaptureImageView(Bitmap image)
        {
            this.image=image;AutoScroll=true;TabStop=true;BackColor=Color.FromArgb(235,239,244);AccessibleName="Vista de la captura";
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        }
        internal void Fit()
        {
            fit=true;AutoScrollMinSize=Size.Empty;AutoScrollPosition=Point.Empty;RefreshView();
        }
        internal void SetZoom(double value)
        {
            Rectangle before=ImageBounds;double previous=Zoom;
            double centerX=(ClientSize.Width/2.0-before.Left)/previous,centerY=(ClientSize.Height/2.0-before.Top)/previous;
            fit=false;zoom=Math.Max(0.01,Math.Min(8,value));
            AutoScrollMinSize=new Size((int)Math.Ceiling(image.Width*zoom)+ImageMargin*2,(int)Math.Ceiling(image.Height*zoom)+ImageMargin*2);
            AutoScrollPosition=new Point(Math.Max(0,(int)(centerX*zoom+ImageMargin-ClientSize.Width/2.0)),Math.Max(0,(int)(centerY*zoom+ImageMargin-ClientSize.Height/2.0)));
            RefreshView();
        }
        private Rectangle ImageBounds
        {
            get
            {
                int width=Math.Max(1,(int)Math.Round(image.Width*Zoom)),height=Math.Max(1,(int)Math.Round(image.Height*Zoom));
                return new Rectangle(width+ImageMargin*2<=ClientSize.Width?(ClientSize.Width-width)/2:ImageMargin+AutoScrollPosition.X,height+ImageMargin*2<=ClientSize.Height?(ClientSize.Height-height)/2:ImageMargin+AutoScrollPosition.Y,width,height);
            }
        }
        private void RefreshView(){Invalidate();if(ViewChanged!=null)ViewChanged();}
        protected override void OnResize(EventArgs e){base.OnResize(e);if(image!=null)RefreshView();}
        protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();}
        protected override void OnMouseWheel(MouseEventArgs e){if((ModifierKeys&Keys.Control)!=0)SetZoom(Zoom*(e.Delta>0?1.25:0.8));else base.OnMouseWheel(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Rectangle bounds=ImageBounds;
            if(bounds.Size==image.Size)e.Graphics.DrawImageUnscaled(image,bounds.Location);
            else{e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;e.Graphics.DrawImage(image,bounds,new Rectangle(Point.Empty,image.Size),GraphicsUnit.Pixel);}
        }
    }
}
