using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Trazo
{
    internal struct ShortcutThreadMessage
    {
        internal uint Message;
        internal IntPtr WParam,LParam;
    }

    internal interface IGlobalShortcutBackend
    {
        uint CurrentThreadId();
        void CreateMessageQueue();
        IntPtr Install(Native.LowLevelKeyboardProc callback);
        void Uninstall(IntPtr hook);
        int GetMessage(out ShortcutThreadMessage message);
        bool PostThreadMessage(uint threadId,uint message,IntPtr wParam,IntPtr lParam);
        int ReadVirtualKey(IntPtr data);
        Keys ReadModifiers();
        bool IsKeyDown(int key);
        IntPtr CallNext(IntPtr hook,int code,IntPtr wParam,IntPtr lParam);
        bool PostCapture(IntPtr targetWindow);
    }

    // Low-level hooks are dispatched to the thread that installs them. Keep this
    // message pump independent of bitmap rendering, clipboard retries and the UI.
    internal sealed class GlobalShortcutListener:IDisposable
    {
        internal const int CaptureMessage=0x8051;
        private const uint QuitMessage=0x0012,EnableMessage=0x8052;
        private readonly IntPtr targetWindow;
        private readonly HashSet<Keys> shortcuts=new HashSet<Keys>();
        private readonly IGlobalShortcutBackend backend;
        private readonly Native.LowLevelKeyboardProc callback;
        private readonly int[] inheritedOwnedKeys;
        private volatile int[] stoppedOwnedKeys=new int[0];
        private readonly object lifecycle=new object();
        private readonly ManualResetEventSlim ready=new ManualResetEventSlim(false);
        private readonly HashSet<int> ownedKeys=new HashSet<int>(),passedKeys=new HashSet<int>();
        private Thread thread;
        private IntPtr hook;
        private volatile uint threadId;
        private volatile bool installed,enabled=true,requestedEnabled=true,stopping,disposed;
        private int readyDisposed;
        internal bool IsInstalled {get{return installed;}}
        internal int[] SnapshotOwnedKeys {get{return (int[])stoppedOwnedKeys.Clone();}}

        internal GlobalShortcutListener(IntPtr targetWindow,Keys[] configured):this(targetWindow,configured,new WindowsGlobalShortcutBackend(),new int[0]) { }
        internal GlobalShortcutListener(IntPtr targetWindow,Keys[] configured,int[] ownedKeys):this(targetWindow,configured,new WindowsGlobalShortcutBackend(),ownedKeys) { }
        internal GlobalShortcutListener(IntPtr targetWindow,Keys[] configured,IGlobalShortcutBackend backend):this(targetWindow,configured,backend,new int[0]) { }
        internal GlobalShortcutListener(IntPtr targetWindow,Keys[] configured,IGlobalShortcutBackend backend,int[] ownedKeys)
        {
            if(configured==null)throw new ArgumentNullException("configured");
            if(backend==null)throw new ArgumentNullException("backend");
            this.targetWindow=targetWindow;this.backend=backend;
            inheritedOwnedKeys=ownedKeys==null?new int[0]:(int[])ownedKeys.Clone();
            foreach(Keys chord in (Keys[])configured.Clone())if(ShortcutSettings.IsValid(chord)&&(chord&ShortcutSettings.WindowsModifier)==0)shortcuts.Add(chord);
            callback=KeyboardHook;
        }
        internal bool Start()
        {
            lock(lifecycle)
            {
                if(disposed)throw new ObjectDisposedException("GlobalShortcutListener");
                if(thread!=null)return installed;
                if(shortcuts.Count==0)return false;
                thread=new Thread(Run){IsBackground=true,Name="Trazo global shortcut listener"};
                thread.SetApartmentState(ApartmentState.STA);
                try{thread.Start();}
                catch{stopping=true;ready.Set();return false;}
                if(!ready.Wait(3000))
                {
                    stopping=true;enabled=false;
                    if(threadId!=0)backend.PostThreadMessage(threadId,QuitMessage,IntPtr.Zero,IntPtr.Zero);
                    return false;
                }
                return installed;
            }
        }
        internal void SetEnabled(bool value)
        {
            lock(lifecycle)
            {
                if(disposed||stopping)return;
                requestedEnabled=value;
                // Suspension takes effect immediately, even if an input message
                // arrives before the queued state reset is processed.
                if(!value)enabled=false;
                if(threadId!=0)backend.PostThreadMessage(threadId,EnableMessage,new IntPtr(value?1:0),IntPtr.Zero);
                else enabled=value;
            }
        }
        private void Run()
        {
            try
            {
                backend.CreateMessageQueue();threadId=backend.CurrentThreadId();
                if(stopping)return;
                SeedHeldKeys();
                hook=backend.Install(callback);installed=hook!=IntPtr.Zero;enabled=requestedEnabled;
                ready.Set();if(!installed)return;
                ShortcutThreadMessage message;
                while(!stopping)
                {
                    int result=backend.GetMessage(out message);if(result<=0||message.Message==QuitMessage)break;
                    if(message.Message==EnableMessage)ApplyEnabled(message.WParam!=IntPtr.Zero);
                }
            }
            catch(Exception error){System.Diagnostics.Debug.WriteLine("Trazo shortcut listener: "+error.GetType().Name);}
            finally
            {
                installed=false;enabled=false;
                try{List<int> remaining=new List<int>();foreach(int key in ownedKeys)if(backend.IsKeyDown(key))remaining.Add(key);stoppedOwnedKeys=remaining.ToArray();}
                catch{stoppedOwnedKeys=new int[0];}
                if(hook!=IntPtr.Zero){try{backend.Uninstall(hook);}catch{}hook=IntPtr.Zero;}
                ownedKeys.Clear();passedKeys.Clear();threadId=0;
                ready.Set();if(disposed)DisposeReady();
            }
        }
        private void SeedHeldKeys()
        {
            foreach(Keys chord in shortcuts){int key=(int)(chord&Keys.KeyCode);if(backend.IsKeyDown(key))passedKeys.Add(key);}
            foreach(int key in inheritedOwnedKeys)if(backend.IsKeyDown(key)){ownedKeys.Add(key);passedKeys.Remove(key);}
        }
        private void ApplyEnabled(bool value)
        {
            if(value)
            {
                // A missed KEYUP cannot leave a shortcut stuck forever. Do not
                // mistake a key still held in the editor for a fresh shortcut.
                RemoveReleasedKeys(ownedKeys);RemoveReleasedKeys(passedKeys);
            }
            else
            {
                passedKeys.Clear();
                // Keep owned releases balanced: a swallowed Print Screen DOWN
                // must not leak its UP to Windows while the editor opens.
            }
            enabled=value&&requestedEnabled&&!stopping;
        }
        private void RemoveReleasedKeys(HashSet<int> keys)
        {
            List<int> released=new List<int>();foreach(int key in keys)if(!backend.IsKeyDown(key))released.Add(key);
            foreach(int key in released)keys.Remove(key);
        }
        private IntPtr KeyboardHook(int code,IntPtr wParam,IntPtr lParam)
        {
            if(code>=0&&!stopping)
            {
                try
                {
                    int key=backend.ReadVirtualKey(lParam),message=wParam.ToInt32();
                    bool down=message==Native.WM_KEYDOWN||message==Native.WM_SYSKEYDOWN,up=message==Native.WM_KEYUP||message==Native.WM_SYSKEYUP;
                    if(up&&ownedKeys.Remove(key))return new IntPtr(1);
                    if(up&&passedKeys.Remove(key))return backend.CallNext(hook,code,wParam,lParam);
                    if(down&&ownedKeys.Contains(key))return new IntPtr(1);
                    if(down&&passedKeys.Contains(key))return backend.CallNext(hook,code,wParam,lParam);
                    Keys modifiers=backend.ReadModifiers();bool active=enabled&&(modifiers&ShortcutSettings.WindowsModifier)==0;
                    if(down)
                    {
                        if(active&&shortcuts.Contains((Keys)key|modifiers)&&backend.PostCapture(targetWindow)){ownedKeys.Add(key);return new IntPtr(1);}
                        passedKeys.Add(key);
                    }
                    // Some keyboards expose Print Screen only as a release.
                    if(up&&key==Native.VK_SNAPSHOT&&active&&shortcuts.Contains((Keys)key|modifiers)&&backend.PostCapture(targetWindow))return new IntPtr(1);
                }
                catch(Exception error){System.Diagnostics.Debug.WriteLine("Trazo shortcut callback: "+error.GetType().Name);}
            }
            return backend.CallNext(hook,code,wParam,lParam);
        }
        public void Dispose()
        {
            lock(lifecycle)
            {
                if(disposed)return;disposed=true;stopping=true;enabled=false;
                if(threadId!=0)backend.PostThreadMessage(threadId,QuitMessage,IntPtr.Zero,IntPtr.Zero);
                if(thread!=null&&thread!=Thread.CurrentThread&&thread.IsAlive)thread.Join(2000);
                if(thread==null||!thread.IsAlive)DisposeReady();
            }
        }
        private void DisposeReady(){if(Interlocked.Exchange(ref readyDisposed,1)==0)ready.Dispose();}
    }

    internal sealed class WindowsGlobalShortcutBackend:IGlobalShortcutBackend
    {
        [StructLayout(LayoutKind.Sequential)]private struct NativeMessage
        {internal IntPtr Window;internal uint Message;internal IntPtr WParam,LParam;internal uint Time;internal System.Drawing.Point Point;internal uint Private;}
        [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll",EntryPoint="PeekMessageW")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PeekMessage(out NativeMessage message,IntPtr window,uint first,uint last,uint remove);
        [DllImport("user32.dll",EntryPoint="GetMessageW")]private static extern int GetNativeMessage(out NativeMessage message,IntPtr window,uint first,uint last);
        [DllImport("user32.dll",EntryPoint="PostThreadMessageW",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PostNativeThreadMessage(uint thread,uint message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll",EntryPoint="PostMessageW",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        public uint CurrentThreadId(){return GetCurrentThreadId();}
        public void CreateMessageQueue(){NativeMessage ignored;PeekMessage(out ignored,IntPtr.Zero,0,0,0);}
        public IntPtr Install(Native.LowLevelKeyboardProc callback){return Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL,callback,Native.GetModuleHandle(null),0);}
        public void Uninstall(IntPtr hook){Native.UnhookWindowsHookEx(hook);}
        public int GetMessage(out ShortcutThreadMessage message)
        {NativeMessage native;int result=GetNativeMessage(out native,IntPtr.Zero,0,0);message=new ShortcutThreadMessage{Message=native.Message,WParam=native.WParam,LParam=native.LParam};return result;}
        public bool PostThreadMessage(uint thread,uint message,IntPtr wParam,IntPtr lParam){return PostNativeThreadMessage(thread,message,wParam,lParam);}
        public int ReadVirtualKey(IntPtr data){return (int)((Native.KeyboardData)Marshal.PtrToStructure(data,typeof(Native.KeyboardData))).vkCode;}
        public Keys ReadModifiers(){return Native.ReadShortcutModifiers();}
        public bool IsKeyDown(int key){return (Native.GetAsyncKeyState(key)&0x8000)!=0;}
        public IntPtr CallNext(IntPtr hook,int code,IntPtr wParam,IntPtr lParam){return Native.CallNextHookEx(hook,code,wParam,lParam);}
        public bool PostCapture(IntPtr targetWindow){return PostMessage(targetWindow,GlobalShortcutListener.CaptureMessage,IntPtr.Zero,IntPtr.Zero);}
    }
}
