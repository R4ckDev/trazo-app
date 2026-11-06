using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Trazo
{
    internal sealed class BarcodeResultsForm:Form
    {
        private Bitmap pendingImage;
        private readonly BackgroundWorker worker=new BackgroundWorker();
        private readonly Label status,format;
        private readonly ProgressBar progress;
        private readonly ListBox resultsList;
        private readonly TextBox content;
        private readonly Button copySelected,copyAll,openLink;
        private readonly List<BarcodeReadResult> results=new List<BarcodeReadResult>();
        private bool started,disposed;

        internal BarcodeResultsForm(Bitmap source)
        {
            if(source==null)throw new ArgumentNullException("source");
            pendingImage=(Bitmap)source.Clone();
            Text="Trazo · Leer QR y códigos de barras";StartPosition=FormStartPosition.Manual;
            ClientSize=new Size(720,520);MinimumSize=new Size(580,380);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Segoe UI",10);BackColor=Color.White;KeyPreview=true;

            TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(16)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,8));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,108));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
            status=new Label{Text="Leyendo la imagen…",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(44,65,85),AccessibleName="Estado de la lectura"};layout.Controls.Add(status,0,0);
            progress=new ProgressBar{Dock=DockStyle.Fill,Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=25,Margin=Padding.Empty,AccessibleName="Lectura en curso"};layout.Controls.Add(progress,0,1);
            resultsList=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,ScrollAlwaysVisible=true,AccessibleName="Códigos encontrados",Margin=new Padding(0,10,0,0)};
            resultsList.SelectedIndexChanged+=delegate{UpdateSelection();};layout.Controls.Add(resultsList,0,2);
            format=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(78,94,110),AccessibleName="Tipo de código seleccionado"};layout.Controls.Add(format,0,3);
            content=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Color.White,Font=new Font("Consolas",10),AccessibleName="Contenido del código seleccionado"};layout.Controls.Add(content,0,4);
            TableLayoutPanel actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Padding=new Padding(0,12,0,0),Margin=Padding.Empty};
            for(int i=0;i<4;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            copySelected=ActionButton("Copiar contenido");copySelected.Click+=delegate{CopyText(GetSelectedText());};actions.Controls.Add(copySelected,0,0);
            copyAll=ActionButton("Copiar todos");copyAll.Click+=delegate{CopyText(GetAllText());};actions.Controls.Add(copyAll,1,0);
            openLink=ActionButton("Abrir enlace");openLink.Click+=delegate{OpenSelectedLink();};actions.Controls.Add(openLink,2,0);
            Button close=ActionButton("Cerrar");close.DialogResult=DialogResult.Cancel;actions.Controls.Add(close,3,0);CancelButton=close;
            layout.Controls.Add(actions,0,5);Controls.Add(layout);UpdateSelection();

            worker.DoWork+=delegate(object sender,DoWorkEventArgs e)
            {
                // The worker owns this separate image. Closing the dialog never releases
                // pixels while the decoder is using them.
                using(Bitmap image=(Bitmap)e.Argument)e.Result=BarcodeDecoder.Decode(image);
            };
            worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e)
            {
                try
                {
                    if(disposed||IsDisposed||Disposing)return;
                    if(e.Error!=null)ApplyError("No se pudo leer la imagen. Vuelve a capturar el código completo y con margen.");
                    else ApplyResults((BarcodeReadResult[])e.Result);
                }
                finally{worker.Dispose();}
            };
        }

        private static Button ActionButton(string text)
        {
            Button button=new Button{Text=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(40,65,90),Margin=new Padding(0,0,7,0),AutoEllipsis=true};
            button.FlatAppearance.BorderColor=Color.FromArgb(191,207,222);return button;
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);Rectangle work=(Owner==null?Screen.FromPoint(Cursor.Position):Screen.FromControl(Owner)).WorkingArea;
            int gap=Math.Min(12,Math.Min(work.Width,work.Height)/12);Rectangle available=Rectangle.Inflate(work,-gap,-gap);
            MinimumSize=new Size(Math.Min(MinimumSize.Width,available.Width),Math.Min(MinimumSize.Height,available.Height));
            Size=new Size(Math.Min(Width,available.Width),Math.Min(Height,available.Height));
            Rectangle center=Owner==null?work:Owner.Bounds;
            Location=new Point(Math.Max(available.Left,Math.Min(available.Right-Width,center.Left+(center.Width-Width)/2)),Math.Max(available.Top,Math.Min(available.Bottom-Height,center.Top+(center.Height-Height)/2)));
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);if(started||disposed)return;started=true;
            Bitmap image=pendingImage;pendingImage=null;
            try{worker.RunWorkerAsync(image);}catch{image.Dispose();ApplyError("No se pudo iniciar la lectura. Cierra esta ventana e inténtalo otra vez.");}
        }
        internal void ApplyResults(BarcodeReadResult[] decoded)
        {
            if(disposed)return;
            results.Clear();resultsList.Items.Clear();progress.Visible=false;
            if(decoded!=null)foreach(BarcodeReadResult result in decoded)
            {
                if(result==null)continue;
                BarcodeReadResult item=new BarcodeReadResult{Format=String.IsNullOrEmpty(result.Format)?"Código":result.Format,Text=result.Text??String.Empty};results.Add(item);
                string preview=item.Text.Replace('\r',' ').Replace('\n',' ').Replace('\0',' ');
                if(preview.Length>90)preview=preview.Substring(0,90)+"…";
                resultsList.Items.Add(item.Format+" · "+preview);
            }
            status.ForeColor=Color.FromArgb(44,65,85);
            status.Text=results.Count==0?"No se encontraron códigos.":results.Count==1?"1 código encontrado.":results.Count+" códigos encontrados. Selecciona uno para ver su contenido.";
            if(results.Count>0)resultsList.SelectedIndex=0;else UpdateSelection();
        }
        internal void ApplyError(string message)
        {
            if(disposed)return;
            results.Clear();resultsList.Items.Clear();progress.Visible=false;status.ForeColor=Color.FromArgb(159,65,42);status.Text=message;UpdateSelection();
        }
        private void UpdateSelection()
        {
            int selected=resultsList.SelectedIndex;bool valid=selected>=0&&selected<results.Count;
            format.Text=valid?"Tipo: "+results[selected].Format:String.Empty;
            content.Text=valid?results[selected].Text:"Selecciona el código completo, con sus márgenes blancos. Si está muy pequeño, acerca la imagen y vuelve a intentarlo.";
            copySelected.Enabled=valid&&!String.IsNullOrEmpty(results[selected].Text);copyAll.Enabled=results.Count>0;
            Uri uri;openLink.Enabled=valid&&TryGetWebUri(results[selected].Text,out uri);
        }
        internal string GetSelectedText()
        {
            int selected=resultsList.SelectedIndex;return selected>=0&&selected<results.Count?results[selected].Text:String.Empty;
        }
        internal string GetAllText()
        {
            StringBuilder text=new StringBuilder();
            foreach(BarcodeReadResult result in results){if(text.Length>0)text.Append("\r\n\r\n");text.Append('[').Append(result.Format).Append("]\r\n").Append(result.Text);}
            return text.ToString();
        }
        internal static bool TryGetWebUri(string text,out Uri uri)
        {
            uri=null;if(String.IsNullOrWhiteSpace(text)||text!=text.Trim()||text.IndexOfAny(new char[]{'\r','\n','\t','\0'})>=0)return false;
            Uri parsed;if(!Uri.TryCreate(text,UriKind.Absolute,out parsed)||String.IsNullOrEmpty(parsed.Host))return false;
            if(!String.Equals(parsed.Scheme,Uri.UriSchemeHttp,StringComparison.OrdinalIgnoreCase)&&!String.Equals(parsed.Scheme,Uri.UriSchemeHttps,StringComparison.OrdinalIgnoreCase))return false;
            if(!text.StartsWith("http://",StringComparison.OrdinalIgnoreCase)&&!text.StartsWith("https://",StringComparison.OrdinalIgnoreCase))return false;
            uri=parsed;return true;
        }
        private void CopyText(string text)
        {
            if(String.IsNullOrEmpty(text))return;
            try{Clipboard.SetDataObject(text,true,8,80);}catch(Exception error){MessageBox.Show(this,"No se pudo copiar el contenido.\n"+error.Message,"Trazo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private void OpenSelectedLink()
        {
            Uri uri;if(!TryGetWebUri(GetSelectedText(),out uri))return;
            try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}
            catch(Exception error){MessageBox.Show(this,"No se pudo abrir el enlace.\n"+error.Message,"Trazo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!disposed)
            {
                disposed=true;if(pendingImage!=null){pendingImage.Dispose();pendingImage=null;}
                if(!worker.IsBusy)worker.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
