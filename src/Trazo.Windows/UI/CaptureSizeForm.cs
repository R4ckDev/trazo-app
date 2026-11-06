using System;
using System.Drawing;
using System.Windows.Forms;

namespace Trazo
{
    internal sealed class CaptureSizeForm:Form
    {
        private readonly ComboBox mode,preset,ratio;
        private readonly NumericUpDown width,height,ratioWidth,ratioHeight;
        private readonly Label error;
        private readonly Size available;
        internal CaptureSelectionConstraint Result {get;private set;}
        internal CaptureSizeForm(CaptureSelectionConstraint current,Size selection,Size available)
        {
            this.available=available;Result=current;
            Text="Tamaño y proporción · Trazo";StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(470,395);Font=new Font("Segoe UI",10);BackColor=Color.White;
            Controls.Add(Label("Modo de selección",22,17,425));
            mode=Combo(22,44,425,new string[]{"Libre","Tamaño exacto en píxeles","Proporción fija"});
            Controls.Add(Label("Tamaño predefinido",22,84,425));
            preset=Combo(22,110,425,new string[]{"Personalizado","1920 × 1080","1280 × 720","1080 × 1080","1080 × 1920","800 × 600"});preset.SelectedIndex=0;
            Controls.Add(Label("Ancho (px)",22,149,185));Controls.Add(Label("Alto (px)",251,149,185));
            width=Number(22,175,196,100000);height=Number(251,175,196,100000);
            width.Value=current.Mode==CaptureSizeMode.Exact?current.Width:Math.Max(4,selection.Width);height.Value=current.Mode==CaptureSizeMode.Exact?current.Height:Math.Max(4,selection.Height);
            Controls.Add(Label("Proporción (ancho : alto)",22,216,425));
            ratio=Combo(22,243,205,new string[]{"1:1 · Cuadrada","4:3 · Horizontal","16:9 · Horizontal","9:16 · Vertical","Personalizada"});
            ratioWidth=Number(247,243,85,10000);ratioHeight=Number(362,243,85,10000);Controls.Add(Label(":",340,246,20));
            ratioWidth.Value=current.Mode==CaptureSizeMode.Ratio?current.Width:16;ratioHeight.Value=current.Mode==CaptureSizeMode.Ratio?current.Height:9;
            ratio.SelectedIndex=4;
            error=Label("Disponible: "+available.Width+" × "+available.Height+" px. No se escala la imagen.",22,283,425);error.Height=47;error.ForeColor=Color.FromArgb(105,119,134);
            Button cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(230,346,103,32)};
            Button apply=new Button{Text="Aplicar",Bounds=new Rectangle(344,346,103,32)};apply.Click+=delegate{Apply();};Controls.Add(cancel);Controls.Add(apply);AcceptButton=apply;CancelButton=cancel;
            preset.SelectedIndexChanged+=delegate{int[,] values={{0,0},{1920,1080},{1280,720},{1080,1080},{1080,1920},{800,600}};if(preset.SelectedIndex>0){width.Value=values[preset.SelectedIndex,0];height.Value=values[preset.SelectedIndex,1];}};
            ratio.SelectedIndexChanged+=delegate{int[,] values={{1,1},{4,3},{16,9},{9,16}};if(ratio.SelectedIndex<4){ratioWidth.Value=values[ratio.SelectedIndex,0];ratioHeight.Value=values[ratio.SelectedIndex,1];}UpdateControls();};
            mode.SelectedIndexChanged+=delegate{UpdateControls();};mode.SelectedIndex=(int)current.Mode;UpdateControls();
        }
        private void UpdateControls()
        {bool exact=mode.SelectedIndex==1,proportion=mode.SelectedIndex==2;preset.Enabled=width.Enabled=height.Enabled=exact;ratio.Enabled=proportion;ratioWidth.Enabled=ratioHeight.Enabled=proportion&&ratio.SelectedIndex==4;}
        private void Apply()
        {
            CaptureSizeMode chosen=(CaptureSizeMode)mode.SelectedIndex;
            CaptureSelectionConstraint result=new CaptureSelectionConstraint(chosen,(int)(chosen==CaptureSizeMode.Ratio?ratioWidth.Value:width.Value),(int)(chosen==CaptureSizeMode.Ratio?ratioHeight.Value:height.Value));
            if(!result.Fits(available)){error.Text="Estas medidas no caben. El área disponible es "+available.Width+" × "+available.Height+" px.";error.ForeColor=Color.Firebrick;return;}
            if(chosen==CaptureSizeMode.Exact&&(result.Width<4||result.Height<4)){error.Text="El tamaño mínimo de captura es 4 × 4 px.";error.ForeColor=Color.Firebrick;return;}
            Result=result;DialogResult=DialogResult.OK;Close();
        }
        private Label Label(string text,int x,int y,int width){Label label=new Label{Text=text,Bounds=new Rectangle(x,y,width,25)};return label;}
        private ComboBox Combo(int x,int y,int width,string[] items){ComboBox c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(x,y,width,28)};c.Items.AddRange(items);Controls.Add(c);return c;}
        private NumericUpDown Number(int x,int y,int width,int maximum){NumericUpDown n=new NumericUpDown{Minimum=1,Maximum=maximum,Bounds=new Rectangle(x,y,width,28),ThousandsSeparator=true};Controls.Add(n);return n;}
    }
}
