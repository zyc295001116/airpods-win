using AirPodsWinTray.Core.Models;

namespace AirPodsWinTray.Core.Parsing;

/// <summary>
/// Apple Continuity proximity beacon parser (OpenPods-compatible field layout).
/// </summary>
public static class AppleProximityParser
{
    public const int AppleCompanyId = 0x004C;
    public const int ExpectedPayloadLength = 27;

    public static ProximityBeaconState ParseManufacturerData(ReadOnlySpan<byte> data)
    {
        var payload = NormalizePayload(data);
        if (payload.Length < ExpectedPayloadLength)
        {
            return new ProximityBeaconState { Parsed = false, RawHex = Convert.ToHexString(data) };
        }

        var status = Convert.ToHexString(payload);
        if (status.Length < 32)
        {
            return new ProximityBeaconState { Parsed = false, RawHex = status };
        }

        var flip = IsFlipped(status);

        var leftNibble = HexNibble(status, flip ? 12 : 13);
        var rightNibble = HexNibble(status, flip ? 13 : 12);
        var caseNibble = HexNibble(status, 15);
        var singleNibble = HexNibble(status, 13);
        var chargeNibble = HexNibble(status, 14);
        var inEarNibble = HexNibble(status, 11);

        var idFull = status.Length >= 10 ? status.Substring(6, 4) : "0000";
        var modelCode = ushort.TryParse(idFull, System.Globalization.NumberStyles.HexNumber, null, out var code)
            ? code
            : (ushort)0;

        var mono = idFull is "0A20" or "1F20" || (status.Length > 7 && status[7] == 'A');

        PodBatteryInfo MakePod(int nibble, bool charging, bool inEar) =>
            new()
            {
                LevelPercent = PodBatteryInfo.NibbleToPercent(nibble),
                IsCharging = charging,
                InEar = inEar
            };

        var chargeL = (chargeNibble & (flip ? 0b10 : 0b01)) != 0;
        var chargeR = (chargeNibble & (flip ? 0b01 : 0b10)) != 0;
        var chargeCase = (chargeNibble & 0b100) != 0;

        var inEarL = (inEarNibble & 0b10) != 0;
        var inEarR = (inEarNibble & 0b1000) != 0;

        if (mono)
        {
            return new ProximityBeaconState
            {
                Parsed = true,
                ModelCode = modelCode,
                IsMono = true,
                Single = MakePod(singleNibble, (chargeNibble & 0b01) != 0, false),
                RawHex = status
            };
        }

        return new ProximityBeaconState
        {
            Parsed = true,
            ModelCode = modelCode,
            IsMono = false,
            Left = MakePod(leftNibble, chargeL, inEarL),
            Right = MakePod(rightNibble, chargeR, inEarR),
            Case = MakePod(caseNibble, chargeCase, false),
            RawHex = status
        };
    }

    private static ReadOnlySpan<byte> NormalizePayload(ReadOnlySpan<byte> data)
    {
        if (data.Length == ExpectedPayloadLength)
        {
            return data;
        }

        if (data.Length >= ExpectedPayloadLength + 2 &&
            data[0] == (AppleCompanyId & 0xFF) &&
            data[1] == (AppleCompanyId >> 8))
        {
            return data.Slice(2, ExpectedPayloadLength);
        }

        return data;
    }

    private static bool IsFlipped(string status)
    {
        if (status.Length <= 10)
        {
            return false;
        }

        var flipNibble = HexNibble(status, 10);
        return (flipNibble & 0b10) == 0;
    }

    private static int HexNibble(string status, int index)
    {
        if (index < 0 || index >= status.Length)
        {
            return 15;
        }

        return Convert.ToInt32(status[index].ToString(), 16);
    }
}