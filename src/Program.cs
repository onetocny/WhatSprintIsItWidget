using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace WhatSprintIsItWidget
{
    /// <summary>
    /// Out-of-process COM server entry point. The Widgets host activates the
    /// registered CLSID; we register a class factory for it and keep the process
    /// alive while the host holds it. The host terminates the process when no
    /// widgets from this provider are in use.
    /// </summary>
    public static class Program
    {
        [MTAThread]
        public static void Main(string[] args)
        {
            // The host launches the provider with "-RegisterProcessAsComServer".
            if (!Array.Exists(args, a =>
                    string.Equals(a, "-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            try
            {
                Guid clsid = typeof(WidgetProvider).GUID;
                var factory = new WidgetProviderFactory();

                ClassObject.Register(clsid, factory, out uint cookie);
                ComLog.Write($"Registered class {clsid:B} cookie={cookie}; waiting for host.");

                // Keep the process alive. The Widgets host owns the lifetime of
                // this out-of-process server and terminates it when it is no
                // longer needed. This event is intentionally never signaled.
                new ManualResetEvent(false).WaitOne();

                ClassObject.Revoke(cookie);
            }
            catch (Exception ex)
            {
                ComLog.Write("FATAL: " + ex);
                throw;
            }
        }
    }

    /// <summary>Lightweight diagnostic log written to %LOCALAPPDATA%\WhatSprintIsItWidget\com.log.</summary>
    internal static class ComLog
    {
        public static void Write(string message)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WhatSprintIsItWidget");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "com.log"),
                    $"{DateTime.Now:O} [{Environment.ProcessId}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostics only; never let logging break the server.
            }
        }
    }

    internal static class ClassObject
    {
        private const int CLSCTX_LOCAL_SERVER = 0x4;
        private const int REGCLS_MULTIPLEUSE = 0x1;

        public static void Register(Guid clsid, object pUnk, out uint cookie)
        {
            int hr = CoRegisterClassObject(clsid, pUnk, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, out cookie);
            ComLog.Write($"CoRegisterClassObject({clsid:B}) hr=0x{hr:X8} cookie={cookie}");
            Marshal.ThrowExceptionForHR(hr);
        }

        public static void Revoke(uint cookie) => CoRevokeClassObject(cookie);

        [DllImport("ole32.dll")]
        private static extern int CoRegisterClassObject(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            int dwClsContext,
            int flags,
            out uint lpdwRegister);

        [DllImport("ole32.dll")]
        private static extern int CoRevokeClassObject(uint dwRegister);
    }

    [ComImport]
    [ComVisible(false)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000001-0000-0000-C000-000000000046")]
    internal interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

        [PreserveSig]
        int LockServer(bool fLock);
    }

    /// <summary>
    /// Non-generic COM class factory for the widget provider. Generic types
    /// cannot expose a COM callable wrapper, so a concrete factory is required.
    /// IWidgetProvider is a WinRT interface, so the C#/WinRT marshaler is used to
    /// produce an IInspectable-based CCW that correctly exposes the WinRT vtable.
    /// </summary>
    [ComVisible(true)]
    internal sealed class WidgetProviderFactory : IClassFactory
    {
        private static readonly Guid IID_IUnknown = new("00000000-0000-0000-C000-000000000046");
        private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
        private const int E_NOINTERFACE = unchecked((int)0x80004002);

        public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;

            if (pUnkOuter != IntPtr.Zero)
            {
                return CLASS_E_NOAGGREGATION;
            }

            try
            {
                if (riid == typeof(WidgetProvider).GUID ||
                    riid == typeof(IWidgetProvider).GUID ||
                    riid == IID_IUnknown)
                {
                    ppvObject = MarshalInspectable<IWidgetProvider>.FromManaged(new WidgetProvider());
                    ComLog.Write($"CreateInstance riid={riid:B} -> OK");
                    return 0;
                }

                ComLog.Write($"CreateInstance riid={riid:B} -> E_NOINTERFACE");
                return E_NOINTERFACE;
            }
            catch (Exception ex)
            {
                ComLog.Write("CreateInstance FAILED: " + ex);
                return ex.HResult != 0 ? ex.HResult : unchecked((int)0x80004005); // E_FAIL
            }
        }

        public int LockServer(bool fLock) => 0;
    }
}
