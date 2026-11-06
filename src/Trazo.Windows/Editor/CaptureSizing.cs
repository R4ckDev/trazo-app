using System;
using System.Drawing;
using System.Windows.Forms;

namespace Trazo
{
    internal sealed partial class OverlayForm
    {
        private CaptureSelectionConstraint captureConstraint=CaptureSelectionConstraint.Free();
        private Button sizeButton,readCodesButton;
        private void InitializeCaptureSizing()
        {
            sizeButton=new Button{Text="Tamaño y proporción",Size=new Size(205,34),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(45,60,75),Cursor=Cursors.Hand,AccessibleName="Tamaño y proporción de la captura (Ctrl+G)",TabStop=true};
            sizeButton.FlatAppearance.BorderColor=Color.FromArgb(190,208,219);sizeButton.Click+=delegate{ShowCaptureSize();};Controls.Add(sizeButton);tips.SetToolTip(sizeButton,"Tamaño exacto y proporción de captura (Ctrl+G)");
            readCodesButton=new Button{Text="Leer QR y barras",Size=new Size(165,34),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(0,139,220),ForeColor=Color.White,Cursor=Cursors.Hand,AccessibleName="Leer QR y códigos de barras (Ctrl+Mayús+R)",Visible=false};
            readCodesButton.FlatAppearance.BorderSize=0;readCodesButton.Click+=delegate{ReadCodes();};Controls.Add(readCodesButton);tips.SetToolTip(readCodesButton,"Leer los códigos del área seleccionada");
        }
        private void PlaceSizeButton()
        {
            if(sizeButton==null)return;
            Point reference=ready?new Point(selection.Right-1,selection.Bottom-1):pointer;
            Rectangle viewport=Screen.FromPoint(new Point(Left+reference.X,Top+reference.Y)).Bounds;viewport.Offset(-Left,-Top);viewport.Intersect(ClientRectangle);if(viewport.Width<1||viewport.Height<1)viewport=ClientRectangle;
            sizeButton.Text=captureConstraint.Description;sizeButton.Location=ready?new Point(Math.Max(viewport.Left+8,viewport.Right-sizeButton.Width-12),viewport.Top+12):new Point(Math.Max(viewport.Left+4,viewport.Left+(viewport.Width-sizeButton.Width)/2),viewport.Top+70);
            sizeButton.Visible=dragMode==0;sizeButton.BringToFront();
            readCodesButton.Location=new Point(Math.Max(viewport.Left+8,sizeButton.Left-readCodesButton.Width-8),sizeButton.Top);
            readCodesButton.Visible=ready&&dragMode==0;readCodesButton.BringToFront();
        }
        private void ShowCaptureSize()
        {
            CommitText();
            Rectangle viewport=Screen.FromPoint(new Point(Left+pointer.X,Top+pointer.Y)).Bounds;viewport.Offset(-Left,-Top);viewport.Intersect(new Rectangle(Point.Empty,screenshot.Size));
            Rectangle initial=ready?selection:new Rectangle(viewport.Left+(viewport.Width-Math.Min(800,viewport.Width))/2,viewport.Top+(viewport.Height-Math.Min(450,viewport.Height))/2,Math.Min(800,viewport.Width),Math.Min(450,viewport.Height));
            using(CaptureSizeForm dialog=new CaptureSizeForm(captureConstraint,initial.Size,screenshot.Size))
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK){Focus();return;}
                ApplyCaptureConstraint(dialog.Result,initial);
            }
            Focus();
        }
        internal void ApplyCaptureConstraint(CaptureSelectionConstraint constraint,Rectangle initial)
        {
            if(constraint==null||!constraint.Fits(screenshot.Size))throw new ArgumentException("El tamaño no cabe en la captura.","constraint");
            Rectangle target=constraint.Apply(initial,screenshot.Size);
            if((ready||constraint.Mode==CaptureSizeMode.Exact)&&(target.Width<4||target.Height<4))throw new ArgumentException("El tamaño mínimo es 4 × 4 px.","constraint");
            captureConstraint=constraint;
            if(ready||constraint.Mode==CaptureSizeMode.Exact)
            {
                selection=target;ready=true;selectedId=null;ActivateSelectionTool();
            }
            UpdateHoverRegion();PlaceBars();Invalidate();
        }
    }
}
