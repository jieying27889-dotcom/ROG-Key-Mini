using HidSharp;
using HidSharp.Reports;
using RogKeyMini.Logging;
using System.Text;

namespace RogKeyMini.Asus;

public sealed class AsusHidService
{
    private const int AsusVendorId = 0x0B05;
    private const byte InputReportId = 0x5A;

    private static readonly int[] KnownAuraProductIds =
    [
        0x1A30, 0x1854, 0x1869, 0x1866, 0x19B6, 0x1822, 0x1837, 0x184A, 0x183D,
        0x8502, 0x1807, 0x17E0, 0x1ABE, 0x1B4C, 0x1B6E, 0x1B2C, 0x8854, 0x1CE7, 0x18C6
    ];

    private readonly LogService _logService;

    public AsusHidService(LogService logService)
    {
        _logService = logService;
    }

    public bool TrySetKeyboardBacklightLevel(int level)
    {
        var safeLevel = Math.Clamp(level, 0, 3);

        try
        {
            var allCandidates = GetAsusInputReportCandidates().ToList();

            if (allCandidates.Count == 0)
            {
                _logService.Warn("No openable ASUS HID device exposing feature report 0x5A was found.");
                return false;
            }

            var knownCandidates = allCandidates
                .Where(device => KnownAuraProductIds.Contains(device.ProductID))
                .ToList();

            var fallbackCandidates = allCandidates
                .Where(device => !KnownAuraProductIds.Contains(device.ProductID))
                .ToList();

            if (TryWriteToFirstWorkingDevice(knownCandidates, safeLevel, "known ASUS Aura PID"))
            {
                return true;
            }

            if (knownCandidates.Count > 0)
            {
                _logService.Warn("Known ASUS Aura HID candidates existed, but none accepted keyboard backlight payload.");
            }

            if (TryWriteToFirstWorkingDevice(fallbackCandidates, safeLevel, "broad ASUS 0x5A fallback"))
            {
                return true;
            }

            _logService.Warn($"Keyboard backlight HID level {safeLevel} failed on all candidate devices.");
            return false;
        }
        catch (Exception ex)
        {
            _logService.Error("Keyboard backlight HID fallback threw an exception.", ex);
            return false;
        }
    }

    private IEnumerable<HidDevice> GetAsusInputReportCandidates()
    {
        IEnumerable<HidDevice> devices;

        try
        {
            devices = DeviceList.Local.GetHidDevices(AsusVendorId).ToList();
        }
        catch (Exception ex)
        {
            _logService.Error("Enumerating ASUS HID devices failed.", ex);
            yield break;
        }

        foreach (var device in devices)
        {
            if (IsUsableInputReportDevice(device))
            {
                yield return device;
            }
        }
    }

    private bool IsUsableInputReportDevice(HidDevice device)
    {
        try
        {
            if (!device.TryOpen(out HidStream? probeStream))
            {
                return false;
            }

            probeStream.Dispose();

            var featureLength = device.GetMaxFeatureReportLength();
            if (featureLength < 5)
            {
                return false;
            }

            var descriptor = device.GetReportDescriptor();
            if (!descriptor.TryGetReport(ReportType.Feature, InputReportId, out _))
            {
                return false;
            }

            var knownTag = KnownAuraProductIds.Contains(device.ProductID) ? "known" : "fallback";
            _logService.Info(
                $"ASUS HID {knownTag} candidate found. PID=0x{device.ProductID:X4}, FeatureLength={featureLength}, Path={device.DevicePath}");

            return true;
        }
        catch (Exception ex)
        {
            _logService.Warn($"Skipping ASUS HID device PID=0x{device.ProductID:X4}: {ex.Message}");
            return false;
        }
    }

    private bool TryWriteToFirstWorkingDevice(
        IReadOnlyCollection<HidDevice> devices,
        int safeLevel,
        string candidateGroup)
    {
        if (devices.Count == 0)
        {
            return false;
        }

        foreach (var device in devices)
        {
            if (TryPrimeAndSetBrightness(device, safeLevel, candidateGroup))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryPrimeAndSetBrightness(HidDevice device, int safeLevel, string candidateGroup)
    {
        try
        {
            var primer = BuildInputPrimerPayload();
            var brightnessPayload = BuildBrightnessPayload(safeLevel);

            using var stream = device.Open();

            if (!TrySetFeature(stream, device, primer, "HID input primer"))
            {
                return false;
            }

            if (!TrySetFeature(stream, device, brightnessPayload, $"keyboard backlight level {safeLevel}"))
            {
                return false;
            }

            _logService.Info(
                $"Keyboard backlight HID level {safeLevel} succeeded through {candidateGroup}. PID=0x{device.ProductID:X4}");

            return true;
        }
        catch (Exception ex)
        {
            _logService.Warn(
                $"Keyboard backlight HID write failed. PID=0x{device.ProductID:X4}, Group={candidateGroup}, Error={ex.Message}");

            return false;
        }
    }

    private bool TrySetFeature(HidStream stream, HidDevice device, byte[] data, string label)
    {
        try
        {
            var featureLength = device.GetMaxFeatureReportLength();
            if (featureLength < data.Length)
            {
                _logService.Warn(
                    $"ASUS HID feature length too small for {label}. PID=0x{device.ProductID:X4}, FeatureLength={featureLength}, PayloadLength={data.Length}");
                return false;
            }

            var payload = new byte[featureLength];
            Array.Copy(data, payload, data.Length);

            stream.SetFeature(payload);
            _logService.Info(
                $"ASUS HID SetFeature succeeded for {label}. PID=0x{device.ProductID:X4}, Payload={BitConverter.ToString(data)}");

            return true;
        }
        catch (Exception ex)
        {
            _logService.Warn(
                $"ASUS HID SetFeature failed for {label}. PID=0x{device.ProductID:X4}, Payload={BitConverter.ToString(data)}, Error={ex.Message}");
            return false;
        }
    }

    private static byte[] BuildInputPrimerPayload()
    {
        var ascii = Encoding.ASCII.GetBytes("ASUS Tech.Inc.");
        var payload = new byte[1 + ascii.Length];

        payload[0] = InputReportId;
        Array.Copy(ascii, 0, payload, 1, ascii.Length);

        return payload;
    }

    private static byte[] BuildBrightnessPayload(int safeLevel)
    {
        return
        [
            InputReportId,
            0xBA,
            0xC5,
            0xC4,
            (byte)safeLevel
        ];
    }
}
