using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;

namespace Trazo
{
    internal static class GlobalShortcutListenerTests
    {
        private static int count;
        private static void Check(bool condition,string text){count++;if(!condition)throw new Exception("Global listener: "+text);}
        internal static int Run()
        {
            count=0;TestIndependentPump();TestKeyboardState();TestSuspendAndRecovery();TestHeldTransfer();TestFailureAndSnapshot();TestNativeThreadQueue();return count;
        }
        private static void TestNativeThreadQueue()
        {
            // Exercise the real Win32 queue ABI, without installing a keyboard
            // hook, registering any shortcut, creating a window or sending input.
            WindowsGlobalShortcutBackend backend=new WindowsGlobalShortcutBackend();uint threadId=0;
            ShortcutThreadMessage received=new ShortcutThreadMessage();int first=0,last=-1;Exception failure=null;
            using(ManualResetEventSlim ready=new ManualResetEventSlim(false))using(ManualResetEventSlim delivered=new ManualResetEventSlim(false))
            {
                Thread pump=new Thread(delegate()
                {
                    try{backend.CreateMessageQueue();threadId=backend.CurrentThreadId();ready.Set();first=backend.GetMessage(out received);delivered.Set();ShortcutThreadMessage quit;last=backend.GetMessage(out quit);}
                    catch(Exception error){failure=error;ready.Set();delivered.Set();}
                }){IsBackground=true};
                pump.Start();
                try
                {
                    Check(ready.Wait(2000)&&failure==null&&threadId!=0,"native worker creates a thread queue without opening a window");
                    Check(backend.PostThreadMessage(threadId,0x8123,new IntPtr(17),new IntPtr(29)),"native command posts without a UI-thread dependency");
                    Check(delivered.Wait(2000)&&failure==null&&first>0&&received.Message==0x8123&&received.WParam==new IntPtr(17)&&received.LParam==new IntPtr(29),"native message layout preserves command and parameters");
                    backend.PostThreadMessage(threadId,0x0012,IntPtr.Zero,IntPtr.Zero);
                    Check(pump.Join(2000)&&failure==null&&last==0,"native queue exits cleanly on a quit command");
                }
                finally{if(pump.IsAlive&&threadId!=0){backend.PostThreadMessage(threadId,0x0012,IntPtr.Zero,IntPtr.Zero);pump.Join(2000);}}
            }
        }
        private static void TestIndependentPump()
        {
            using(FakeBackend backend=new FakeBackend())using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(42),new Keys[]{Keys.PrintScreen},backend))
            {
                int caller=Thread.CurrentThread.ManagedThreadId;Check(listener.Start()&&listener.IsInstalled,"starts a dedicated listener");
                Check(backend.InstallThread!=caller,"hook is installed outside the UI caller thread");
                Work input=backend.EnqueueKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);
                // Simulate a long clipboard/render operation on the calling UI thread.
                Thread.Sleep(1200);
                Check(input.Done.IsSet&&backend.Captures==1,"input is processed while caller is blocked beyond Windows' hook timeout");
                Check(backend.CallbackThread==backend.InstallThread&&backend.PostThread==backend.InstallThread,"callback and asynchronous capture post stay on the hook thread");
                Check(backend.LastTarget==new IntPtr(42),"posts capture to the requested dashboard window");input.Done.Dispose();
                Check(backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None)==new IntPtr(1),"matching release is swallowed");
                listener.Dispose();listener.Dispose();Check(!listener.IsInstalled&&backend.UninstallCount==1,"dispose stops the pump and unhooks exactly once");
            }
        }
        private static void TestKeyboardState()
        {
            using(FakeBackend backend=new FakeBackend())using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),new Keys[]{Keys.PrintScreen,Keys.Control|Keys.Q},backend))
            {
                Check(listener.Start(),"keyboard-state listener starts");
                Check(backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None)==new IntPtr(1),"Print Screen down triggers capture");
                backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);
                Check(backend.Captures==1,"auto-repeat does not schedule additional captures");
                backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);
                Check(backend.Captures==2,"next physical press works after releasing the previous capture key");
                backend.SendKey((int)Keys.Q,Native.WM_KEYDOWN,Keys.Control);backend.SendKey((int)Keys.Q,Native.WM_KEYUP,Keys.Control);
                Check(backend.Captures==3,"configured modifier combination matches on the worker");
                IntPtr pass=backend.SendKey((int)Keys.Q,Native.WM_KEYDOWN,Keys.Control|Keys.Shift);backend.SendKey((int)Keys.Q,Native.WM_KEYUP,Keys.Control|Keys.Shift);
                Check(pass==FakeBackend.NextResult&&backend.Captures==3,"extra modifiers are passed to other applications");
                backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);
                Check(backend.Captures==5,"release-only Print Screen keyboards can capture repeatedly");
            }
        }
        private static void TestSuspendAndRecovery()
        {
            using(FakeBackend backend=new FakeBackend())using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),new Keys[]{Keys.PrintScreen,Keys.H},backend))
            {
                Check(listener.Start(),"suspend listener starts");
                backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);listener.SetEnabled(false);
                Check(backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None)==new IntPtr(1),"suspend preserves the swallowed trigger's release balance");
                Check(backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None)==FakeBackend.NextResult,"editor keystrokes pass through while capture is active");
                listener.SetEnabled(true);backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None);
                Check(backend.Captures==1,"a key held inside the editor does not trigger when shortcuts resume");
                backend.SendKey((int)Keys.H,Native.WM_KEYUP,Keys.None);backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.H,Native.WM_KEYUP,Keys.None);
                Check(backend.Captures==2,"a new press works after the editor-held key is released");
                backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);backend.SetHardwareKey((int)Keys.PrintScreen,false);
                listener.SetEnabled(false);listener.SetEnabled(true);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);
                Check(backend.Captures==4,"a lost key-up is cleared on resume instead of leaving the shortcut stuck");
                int previous=backend.Captures;
                for(int i=0;i<30;i++)
                {
                    listener.SetEnabled(false);backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.H,Native.WM_KEYUP,Keys.None);
                    listener.SetEnabled(true);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);
                }
                Check(backend.Captures==previous+30,"thirty capture/editor/resume cycles keep the shortcut responsive");
            }
        }
        private static void TestHeldTransfer()
        {
            int[] owned;
            using(FakeBackend backend=new FakeBackend())
            {
                GlobalShortcutListener old=new GlobalShortcutListener(new IntPtr(1),new Keys[]{Keys.PrintScreen},backend);Check(old.Start(),"transfer source starts");
                backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);old.Dispose();owned=old.SnapshotOwnedKeys;
                Check(owned.Length==1&&owned[0]==(int)Keys.PrintScreen,"shutdown returns only outstanding physically held releases");
            }
            using(FakeBackend backend=new FakeBackend())
            {
                backend.SetHardwareKey((int)Keys.PrintScreen,true);
                using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),new Keys[]{Keys.PrintScreen},backend,owned))
                {
                    Check(listener.Start(),"replacement listener starts with transferred releases");
                    backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);Check(backend.Captures==0,"holding the old trigger across listener replacement does not repeat capture");
                    Check(backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None)==new IntPtr(1),"replacement swallows release owed by the old hook");
                    backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.PrintScreen,Native.WM_KEYUP,Keys.None);
                    Check(backend.Captures==1,"a fresh press after transfer triggers normally");
                }
            }
            using(FakeBackend backend=new FakeBackend())
            {
                backend.SetHardwareKey((int)Keys.H,true);
                using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),new Keys[]{Keys.H},backend))
                {
                    listener.Start();backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.H,Native.WM_KEYUP,Keys.None);
                    Check(backend.Captures==0,"startup does not turn an already held unrelated key into a capture");
                }
            }
        }
        private static void TestFailureAndSnapshot()
        {
            Keys[] configured={Keys.H};
            using(FakeBackend backend=new FakeBackend())using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),configured,backend))
            {
                configured[0]=Keys.J;listener.Start();backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.H,Native.WM_KEYUP,Keys.None);
                backend.SendKey((int)Keys.J,Native.WM_KEYDOWN,Keys.None);backend.SendKey((int)Keys.J,Native.WM_KEYUP,Keys.None);
                Check(backend.Captures==1,"constructor uses an independent immutable shortcut snapshot");
                Check(listener.Start()&&backend.InstallCount==1,"calling Start twice does not install duplicate hooks");
                backend.FailPost=true;Check(backend.SendKey((int)Keys.H,Native.WM_KEYDOWN,Keys.None)==FakeBackend.NextResult,"failed capture post does not swallow the user's key");
                backend.SendKey((int)Keys.H,Native.WM_KEYUP,Keys.None);Check(backend.Captures==1,"failed capture post cannot create a phantom request");
            }
            using(FakeBackend backend=new FakeBackend())using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),new Keys[]{Keys.H},backend))
            {backend.FailInstall=true;Check(!listener.Start()&&!listener.IsInstalled,"installation failure is reported to activate native fallback");}
            using(FakeBackend backend=new FakeBackend())using(GlobalShortcutListener listener=new GlobalShortcutListener(new IntPtr(1),new Keys[]{ShortcutSettings.WindowsModifier|Keys.H},backend))
            {Check(!listener.Start()&&backend.InstallCount==0,"Windows-key chords are left to RegisterHotKey");}
        }
        private sealed class Work
        {
            internal bool KeyEvent;
            internal int Key,Message;
            internal Keys Modifiers;
            internal ShortcutThreadMessage ThreadMessage;
            internal readonly ManualResetEventSlim Done=new ManualResetEventSlim(false);
            internal IntPtr Result;
        }
        private sealed class FakeBackend:IGlobalShortcutBackend,IDisposable
        {
            internal static readonly IntPtr NextResult=new IntPtr(77);
            private readonly BlockingCollection<Work> queue=new BlockingCollection<Work>();
            private readonly ConcurrentDictionary<int,bool> hardware=new ConcurrentDictionary<int,bool>();
            private Native.LowLevelKeyboardProc callback;
            private Keys modifiers;
            internal int InstallThread,CallbackThread,PostThread,Captures,InstallCount,UninstallCount;
            internal bool FailInstall,FailPost;
            internal IntPtr LastTarget;
            public uint CurrentThreadId(){return (uint)Thread.CurrentThread.ManagedThreadId;}
            public void CreateMessageQueue(){}
            public IntPtr Install(Native.LowLevelKeyboardProc value){callback=value;InstallThread=Thread.CurrentThread.ManagedThreadId;InstallCount++;return FailInstall?IntPtr.Zero:new IntPtr(1);}
            public void Uninstall(IntPtr hook){Interlocked.Increment(ref UninstallCount);}
            public int GetMessage(out ShortcutThreadMessage message)
            {
                Work work=queue.Take();message=work.ThreadMessage;
                if(work.KeyEvent)
                {
                    modifiers=work.Modifiers;CallbackThread=Thread.CurrentThread.ManagedThreadId;
                    try{work.Result=callback(0,new IntPtr(work.Message),new IntPtr(work.Key));hardware[work.Key]=work.Message==Native.WM_KEYDOWN||work.Message==Native.WM_SYSKEYDOWN;}
                    finally{work.Done.Set();}
                    return 1;
                }
                work.Done.Dispose();return message.Message==0x0012?0:1;
            }
            public bool PostThreadMessage(uint thread,uint message,IntPtr wParam,IntPtr lParam)
            {queue.Add(new Work{ThreadMessage=new ShortcutThreadMessage{Message=message,WParam=wParam,LParam=lParam}});return true;}
            public int ReadVirtualKey(IntPtr data){return data.ToInt32();}
            public Keys ReadModifiers(){return modifiers;}
            public bool IsKeyDown(int key){bool down;return hardware.TryGetValue(key,out down)&&down;}
            public IntPtr CallNext(IntPtr hook,int code,IntPtr wParam,IntPtr lParam){return NextResult;}
            public bool PostCapture(IntPtr window){if(FailPost)return false;LastTarget=window;PostThread=Thread.CurrentThread.ManagedThreadId;Interlocked.Increment(ref Captures);return true;}
            internal void SetHardwareKey(int key,bool down){hardware[key]=down;}
            internal Work EnqueueKey(int key,int message,Keys keys){Work work=new Work{KeyEvent=true,Key=key,Message=message,Modifiers=keys};queue.Add(work);return work;}
            internal IntPtr SendKey(int key,int message,Keys keys)
            {Work work=EnqueueKey(key,message,keys);if(!work.Done.Wait(2000))throw new Exception("Fake hook input timed out");IntPtr result=work.Result;work.Done.Dispose();return result;}
            public void Dispose(){Work pending;while(queue.TryTake(out pending))pending.Done.Dispose();queue.Dispose();}
        }
    }
}
