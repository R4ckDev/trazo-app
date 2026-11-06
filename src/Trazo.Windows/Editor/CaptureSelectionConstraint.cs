using System;
using System.Drawing;

namespace Trazo
{
    internal enum CaptureSizeMode { Free, Exact, Ratio }

    internal sealed class CaptureSelectionConstraint
    {
        internal readonly CaptureSizeMode Mode;
        internal readonly int Width,Height;
        internal CaptureSelectionConstraint(CaptureSizeMode mode,int width,int height)
        {
            if(width<1||height<1)throw new ArgumentOutOfRangeException("width","Las medidas deben ser mayores que cero.");
            Mode=mode;
            if(mode==CaptureSizeMode.Ratio){int divisor=Gcd(width,height);width/=divisor;height/=divisor;}
            Width=width;Height=height;
        }
        internal static CaptureSelectionConstraint Free(){return new CaptureSelectionConstraint(CaptureSizeMode.Free,1,1);}
        private static int Gcd(int a,int b){while(b!=0){int c=a%b;a=b;b=c;}return a;}
        internal bool Fits(Size bounds){return Mode==CaptureSizeMode.Free||(Width<=bounds.Width&&Height<=bounds.Height);}
        internal string Description {get{return Mode==CaptureSizeMode.Free?"Tamaño y proporción":Mode==CaptureSizeMode.Exact?Width+" × "+Height+" px":Width+":"+Height+" · Proporción";}}
        internal Rectangle FromDrag(Point anchor,Point end,Size bounds)
        {
            if(Mode==CaptureSizeMode.Free)return Annotation.Between(anchor,end);
            int sx=end.X<anchor.X?-1:1,sy=end.Y<anchor.Y?-1:1;
            if(Mode==CaptureSizeMode.Exact)return Place(new Point(anchor.X+(sx<0?-Width:0),anchor.Y+(sy<0?-Height:0)),new Size(Width,Height),bounds);
            int maxWidth=sx<0?anchor.X:bounds.Width-anchor.X,maxHeight=sy<0?anchor.Y:bounds.Height-anchor.Y;
            Size size=RatioSize(Math.Abs(end.X-anchor.X),Math.Abs(end.Y-anchor.Y),maxWidth,maxHeight);
            return new Rectangle(anchor.X+(sx<0?-size.Width:0),anchor.Y+(sy<0?-size.Height:0),size.Width,size.Height);
        }
        internal Rectangle Apply(Rectangle current,Size bounds)
        {
            if(Mode==CaptureSizeMode.Free)return current;
            Size size=Mode==CaptureSizeMode.Exact?new Size(Width,Height):RatioSize(current.Width,current.Height,bounds.Width,bounds.Height);
            return Place(new Point(current.Left+(current.Width-size.Width)/2,current.Top+(current.Height-size.Height)/2),size,bounds);
        }
        internal Rectangle Resize(Rectangle current,int handle,Point end,Size bounds)
        {
            if(Mode==CaptureSizeMode.Exact)return current;
            if(Mode!=CaptureSizeMode.Ratio)throw new InvalidOperationException("El tamaño libre no necesita restricción.");
            if(handle==0||handle==2||handle==4||handle==6)
            {
                bool left=handle==0||handle==6,top=handle==0||handle==2;
                Point fixedPoint=new Point(left?current.Right:current.Left,top?current.Bottom:current.Top);
                Point moving=new Point(left?Math.Min(end.X,fixedPoint.X-1):Math.Max(end.X,fixedPoint.X+1),top?Math.Min(end.Y,fixedPoint.Y-1):Math.Max(end.Y,fixedPoint.Y+1));
                Rectangle result=FromDrag(fixedPoint,moving,bounds);return result.Width>0&&result.Height>0?result:current;
            }
            double cx=current.Left+current.Width/2.0,cy=current.Top+current.Height/2.0;
            Size size;
            if(handle==3||handle==7)
            {
                bool left=handle==7;int fixedX=left?current.Right:current.Left;
                int width=left?Math.Max(1,fixedX-end.X):Math.Max(1,end.X-fixedX);
                size=RatioSize(width,0,left?fixedX:bounds.Width-fixedX,(int)(2*Math.Min(cy,bounds.Height-cy)));
                if(size.IsEmpty)return current;
                return Place(new Point(left?fixedX-size.Width:fixedX,(int)Math.Round(cy-size.Height/2.0)),size,bounds);
            }
            bool up=handle==1;int fixedY=up?current.Bottom:current.Top;
            int height=up?Math.Max(1,fixedY-end.Y):Math.Max(1,end.Y-fixedY);
            size=RatioSize(0,height,(int)(2*Math.Min(cx,bounds.Width-cx)),up?fixedY:bounds.Height-fixedY);
            if(size.IsEmpty)return current;
            return Place(new Point((int)Math.Round(cx-size.Width/2.0),up?fixedY-size.Height:fixedY),size,bounds);
        }
        private Size RatioSize(int desiredWidth,int desiredHeight,int maxWidth,int maxHeight)
        {
            int max=Math.Min(maxWidth/Width,maxHeight/Height);if(max<1)return Size.Empty;
            double requested=Math.Max(desiredWidth/(double)Width,desiredHeight/(double)Height);
            int units=Math.Max(1,Math.Min(max,(int)Math.Round(requested)));
            return new Size(units*Width,units*Height);
        }
        private static Rectangle Place(Point point,Size size,Size bounds)
        {return new Rectangle(Math.Max(0,Math.Min(bounds.Width-size.Width,point.X)),Math.Max(0,Math.Min(bounds.Height-size.Height,point.Y)),size.Width,size.Height);}
    }
}
