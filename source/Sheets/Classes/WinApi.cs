using System;
using System.Runtime.InteropServices;
using System.Security;

namespace BIMPlugins.Sheets.Classes
{
    public static class WinApi
    {
        public const int PRINTER_ACCESS_USE = 8;
        public const int PRINTER_ACCESS_ADMINISTER = 4;

        [SuppressUnmanagedCodeSecurity]
        [DllImport("winspool.Drv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern bool OpenPrinter(
            [MarshalAs(UnmanagedType.LPTStr)] string printerName,
            out IntPtr phPrinter,
            ref PrinterDefaults pd);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("winspool.Drv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern bool ClosePrinter(IntPtr phPrinter);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("winspool.Drv", EntryPoint = "AddFowmW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern bool AddForm(IntPtr phPrinter, [MarshalAs(UnmanagedType.I4)] int level, ref FormInfo1 form);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("winspool.Drv", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern bool DeleteForm(IntPtr phPrinter, [MarshalAs(UnmanagedType.LPStr)] string pName);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetLastError();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct PrinterDefaults
        {
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pDatatype;
            public IntPtr pDevMode;
            [MarshalAs(UnmanagedType.I4)]
            public int DesiredAccess;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct Size
        {
            public int width;
            public int height;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct FormInfo1
        {
            public uint Flags;
            public string pName;
            public Size Size;
            public Rect ImageableArea;
        }
    }
}