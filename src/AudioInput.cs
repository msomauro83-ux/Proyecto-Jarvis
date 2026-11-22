using System;
using System.Runtime.InteropServices;

namespace Jarvis
{
    internal static class AudioInput
    {
        public static string DefaultName()
        {
            IDevices enumerator = null;
            IDevice device = null;
            IProperties store = null;
            try
            {
                enumerator = (IDevices)new Enumerator();
                if (enumerator.GetDefaultAudioEndpoint(1, 0, out device) < 0) return "No hay una entrada predeterminada";
                if (device.OpenPropertyStore(0, out store) < 0) return "Micrófono predeterminado de Windows";
                Key key = new Key { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                Value value;
                if (store.GetValue(ref key, out value) < 0) return "Micrófono predeterminado de Windows";
                try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Data) : "Micrófono predeterminado de Windows"; }
                finally { PropVariantClear(ref value); }
            }
            catch (COMException) { return "Micrófono predeterminado de Windows"; }
            catch (InvalidCastException) { return "Micrófono predeterminado de Windows"; }
            finally
            {
                if (store != null) Marshal.ReleaseComObject(store);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class Enumerator { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDevices
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
            [PreserveSig] int OpenPropertyStore(uint access, out IProperties store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out uint state);
        }
        [StructLayout(LayoutKind.Sequential)] private struct Key { public Guid Format; public uint Id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)] private struct Value { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Data; }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IProperties
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out Key key);
            [PreserveSig] int GetValue(ref Key key, out Value value);
            [PreserveSig] int SetValue(ref Key key, ref Value value);
            [PreserveSig] int Commit();
        }
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref Value value);
    }
}
