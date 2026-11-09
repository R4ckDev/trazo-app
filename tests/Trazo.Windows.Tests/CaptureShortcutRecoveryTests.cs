using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Trazo
{
    internal static class CaptureShortcutRecoveryTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static int assertions;

        private static void Check(bool condition,string label)
        {
            assertions++;
            if(!condition)throw new Exception("Capture shortcut recovery: "+label);
        }
        private static void Set(MainForm form,string name,bool value)
        {typeof(MainForm).GetField(name,Flags).SetValue(form,value);}
        private static bool Get(MainForm form,string name)
        {return (bool)typeof(MainForm).GetField(name,Flags).GetValue(form);}
        private static MainForm CreateDashboard()
        {
            return new MainForm(null,new AppSettings(),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"unused-shortcut-recovery-"+Guid.NewGuid().ToString("N")),true);
        }

        internal static int Run()
        {
            assertions=0;
            using(MainForm form=CreateDashboard())
            {
                Check(!form.Visible&&typeof(MainForm).GetField("shortcutListener",Flags).GetValue(form)==null&&((HashSet<int>)typeof(MainForm).GetField("registeredHotkeys",Flags).GetValue(form)).Count==0,"regressions do not show a window or start global input registration");
                Set(form,"capturing",true);Set(form,"captureQueued",true);
                int rearmed=0;
                form.CompleteCaptureOperation(delegate
                {
                    Check(!Get(form,"capturing")&&!Get(form,"captureQueued"),"capture and queued guards are cleared before listeners are rearmed");
                    rearmed++;
                });
                Check(rearmed==1,"a completed capture rearms shortcuts once");
                Check(!Get(form,"capturing")&&!Get(form,"captureQueued"),"completed capture cannot leave a queued request or an active-capture latch");

                Set(form,"capturing",true);Set(form,"captureQueued",true);
                form.CompleteCaptureOperation(delegate{rearmed++;});
                Check(rearmed==2,"a second completed capture rearms again instead of relying on a previous listener handle");

                InvalidOperationException registrationError=new InvalidOperationException("Simulated shortcut registration failure");
                Set(form,"capturing",true);Set(form,"captureQueued",true);
                Exception observed=null;
                try{form.CompleteCaptureOperation(delegate{throw registrationError;});}
                catch(Exception error){observed=error;}
                Check(Object.ReferenceEquals(observed,registrationError),"registration failure remains observable to the caller");
                Check(!Get(form,"capturing")&&!Get(form,"captureQueued"),"even a registration exception cannot permanently block future capture attempts");

                Set(form,"capturing",true);Set(form,"captureQueued",true);Set(form,"exiting",true);
                form.CompleteCaptureOperation(delegate{rearmed++;});
                Check(rearmed==2,"application exit does not install new shortcut listeners");
                Check(!Get(form,"capturing")&&!Get(form,"captureQueued"),"exit also releases pending capture state");
            }
            using(MainForm disposed=CreateDashboard())
            {
                disposed.Dispose();Set(disposed,"exiting",false);Set(disposed,"capturing",true);Set(disposed,"captureQueued",true);
                bool called=false;disposed.CompleteCaptureOperation(delegate{called=true;});
                Check(!called,"a late completion callback cannot rearm a disposed dashboard");
                Check(!Get(disposed,"capturing")&&!Get(disposed,"captureQueued"),"a late completion clears state without touching native input or windows");
            }
            return assertions;
        }
    }
}
