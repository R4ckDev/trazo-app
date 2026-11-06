using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Trazo
{
    internal static class BarcodeResultsUiTests
    {
        private static int count;
        private static void Check(bool condition,string label){count++;if(!condition)throw new Exception(label);}
        private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
        internal static int Run()
        {
            count=0;
            using(Bitmap source=new Bitmap(80,80))
            {
                source.SetPixel(20,20,Color.RoyalBlue);
                using(BarcodeResultsForm form=new BarcodeResultsForm(source))
                {
                    Check(!form.Visible&&!form.IsHandleCreated,"Constructing code results does not open a visible test window");
                    Check(form.WindowState==FormWindowState.Normal&&!form.TopMost&&form.FormBorderStyle==FormBorderStyle.Sizable,"Code results use a normal resizable window");
                    TextBox content=(TextBox)Field(form,"content");
                    Check(content.Multiline&&content.ReadOnly&&!content.WordWrap,"Decoded content is shown in a plain read-only multiline field");
                    Bitmap pending=(Bitmap)Field(form,"pendingImage");source.SetPixel(20,20,Color.Red);
                    Check(pending.GetPixel(20,20).ToArgb()==Color.RoyalBlue.ToArgb(),"Reader dialog owns independent input pixels");
                    form.ApplyResults(new BarcodeReadResult[0]);
                    Check(form.GetSelectedText()==String.Empty&&form.GetAllText()==String.Empty,"No-code result has no content to copy");
                    Check(!((Button)Field(form,"copySelected")).Enabled&&!((Button)Field(form,"copyAll")).Enabled,"No-code result disables copy actions");
                    Check(content.Text.Contains("márgenes blancos"),"No-code result explains how to improve the region");
                    string exact="Español: áéíóú · 中文 · 😀\r\nsegunda línea\ntercera línea";
                    BarcodeReadResult first=new BarcodeReadResult{Format="QR Code",Text=exact};
                    form.ApplyResults(new BarcodeReadResult[]{first,new BarcodeReadResult{Format="CODE_128",Text="ABC-123456"},new BarcodeReadResult{Format="QR Code",Text="https://example.org/path?q=trazo"}});
                    Check(form.GetSelectedText()==exact,"Selected copy preserves Unicode and exact original newline sequences");
                    first.Text="changed after display";Check(form.GetSelectedText()==exact,"Caller mutation cannot change displayed code content");
                    string all=form.GetAllText();
                    Check(all.Contains("[QR Code]\r\n"+exact)&&all.Contains("[CODE_128]\r\nABC-123456"),"Copy-all includes formats and original content for every code");
                    Check(!((Button)Field(form,"openLink")).Enabled,"Plain QR text does not offer opening an application");
                    ListBox list=(ListBox)Field(form,"resultsList");list.SelectedIndex=1;
                    Check(form.GetSelectedText()=="ABC-123456"&&((Label)Field(form,"format")).Text=="Tipo: CODE_128","Selecting another code updates its type and exact copy value");
                    list.SelectedIndex=2;Check(((Button)Field(form,"openLink")).Enabled,"HTTP link is available only as an explicit button");
                    form.ApplyError("No se pudo leer la imagen.");
                    Check(form.GetSelectedText()==String.Empty&&!((Button)Field(form,"openLink")).Enabled&&((Label)Field(form,"status")).Text=="No se pudo leer la imagen.","Reading errors clear stale results and actions");
                    form.ApplyResults(new BarcodeReadResult[]{null,new BarcodeReadResult{Format=null,Text=null}});
                    Check(form.GetSelectedText()==String.Empty&&!((Button)Field(form,"copySelected")).Enabled,"Empty or null result values cannot cause a copy or crash");
                }
                Check(source.GetPixel(20,20).ToArgb()==Color.Red.ToArgb(),"Closing reader dialog does not dispose caller-owned pixels");
            }
            Uri uri;
            Check(BarcodeResultsForm.TryGetWebUri("https://example.org/a?b=c#fragment",out uri)&&uri.Scheme=="https","HTTPS links are recognized");
            Check(BarcodeResultsForm.TryGetWebUri("HTTP://example.org",out uri),"HTTP scheme recognition is case-insensitive");
            string[] rejected={"javascript:alert(1)","file:///C:/Windows/notepad.exe","ms-settings:privacy","mailto:test@example.org","https:example.org","example.org"," https://example.org","https://example.org\nhttps://other.org","cmd.exe /c echo test","data:text/html,<h1>hello</h1>","https://example.org\0"};
            foreach(string text in rejected)Check(!BarcodeResultsForm.TryGetWebUri(text,out uri),"Non-web or ambiguous payload is not executable as a link: "+text.Replace('\0','?'));
            return count;
        }
    }
}
