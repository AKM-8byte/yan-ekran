using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TelefonPaneli.Windows.Sensors;

/// <summary>
/// Optimus dizüstülerde NVIDIA GPU boştayken uykuya geçer. Onu sensör okuyarak uyandırmamak için
/// Windows'un "GPU Engine" sayaçlarına bakarız: NVIDIA adaptörünün motorlarından biri son 5 sn içinde
/// çalıştıysa GPU uyanık sayılır.
/// </summary>
public sealed class GpuActivityGate : IDisposable
{
    static readonly TimeSpan Linger = TimeSpan.FromSeconds(5);
    static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(1);

    readonly bool? _forced;
    readonly string? _luidToken;
    long _lastPoll;
    long _lastActive;
    bool _awake;

    public GpuActivityGate(bool forceAwake = false, bool forceAsleep = false)
    {
        _forced = forceAwake ? true : forceAsleep ? false : null;
        NvidiaLuid = Dxgi.FindNvidiaLuid();
        if (NvidiaLuid is { } l) _luidToken = $"luid_0x{(uint)l.High:X8}_0x{l.Low:X8}";
    }

    /// <summary>NVIDIA adaptörünün LUID'i; NVIDIA GPU yoksa null.</summary>
    public (uint Low, int High)? NvidiaLuid { get; }

    public bool HasNvidia => NvidiaLuid is not null;

    public bool IsAwake
    {
        get
        {
            if (_forced is { } f) return f;
            if (_luidToken is null) return false;
            var now = Stopwatch.GetTimestamp();
            if (_lastPoll != 0 && Stopwatch.GetElapsedTime(_lastPoll, now) < PollEvery) return _awake;
            _lastPoll = now;
            if (AnyEngineBusy()) _lastActive = now;
            _awake = _lastActive != 0 && Stopwatch.GetElapsedTime(_lastActive, now) < Linger;
            return _awake;
        }
    }

    bool AnyEngineBusy()
    {
        try
        {
            var data = new PerformanceCounterCategory("GPU Engine").ReadCategory();
            if (data["Utilization Percentage"] is not { } util) return false;
            foreach (InstanceData inst in util.Values)
            {
                if (inst.RawValue > 0 && inst.InstanceName.Contains(_luidToken!, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        return false;
    }

    public void Dispose() { }

    static class Dxgi
    {
        const uint NvidiaVendor = 0x10DE;

        public static (uint Low, int High)? FindNvidiaLuid()
        {
            var iid = typeof(IDXGIFactory1).GUID;
            if (CreateDXGIFactory1(ref iid, out var factoryObj) != 0 || factoryObj is not IDXGIFactory1 factory) return null;
            try
            {
                for (uint i = 0; factory.EnumAdapters1(i, out var adapter) == 0; i++)
                {
                    try
                    {
                        if (adapter.GetDesc1(out var desc) == 0 && desc.VendorId == NvidiaVendor)
                            return (desc.LuidLow, desc.LuidHigh);
                    }
                    finally { Marshal.ReleaseComObject(adapter); }
                }
            }
            finally { Marshal.ReleaseComObject(factory); }
            return null;
        }

        [DllImport("dxgi.dll")]
        static extern int CreateDXGIFactory1(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct AdapterDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint VendorId, DeviceId, SubSysId, Revision;
            public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
        }

        [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDXGIFactory1
        {
            // IDXGIObject
            void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
            // IDXGIFactory
            void EnumAdapters(); void MakeWindowAssociation(); void GetWindowAssociation(); void CreateSwapChain(); void CreateSoftwareAdapter();
            // IDXGIFactory1
            [PreserveSig] int EnumAdapters1(uint index, out IDXGIAdapter1 adapter);
            [PreserveSig] bool IsCurrent();
        }

        [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDXGIAdapter1
        {
            // IDXGIObject
            void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();
            // IDXGIAdapter
            void EnumOutputs(); void GetDesc(); void CheckInterfaceSupport();
            // IDXGIAdapter1
            [PreserveSig] int GetDesc1(out AdapterDesc1 desc);
        }
    }
}
