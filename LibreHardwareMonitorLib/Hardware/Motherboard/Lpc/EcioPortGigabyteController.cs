// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace LibreHardwareMonitor.Hardware.Motherboard.Lpc;

internal class EcioPortGigabyteController : IGigabyteController
{
    private const ushort ControllerVersionOffset = 0x00;
    private const ushort ControllerEnableRegister = 0x47;
    private const ushort ControllerFanControlArea = 0x900;

    private const ushort ExtraControllerVersionOffset = 0x00;
    private const ushort ExtraControllerFanControlArea = 0xC00;

    private const ushort EcioRegisterPort = 0x3F4;
    private const ushort EcioValuePort = 0x3F0;

    private readonly ushort[] TemperatureOffsets = { 0x2, 0x3 };
    private readonly ushort[] FanOffsets = { 0xA, 0xC };
    private readonly ushort[] ControlOffsets = { 0x8, 0x9 };

    private readonly IT879xEcioPort _port;

    private bool? _initialState;

    private EcioPortGigabyteController(IT879xEcioPort port)
    {
        _port = port;

        // Check extras by querying its version.
        if (port.ReadByte(ExtraControllerFanControlArea + ExtraControllerVersionOffset, out byte majorVersion) && majorVersion == 1)
        {
            ExtraControls = new float?[ControlOffsets.Length];
            ExtraFans = new float?[FanOffsets.Length];
            ExtraTemperatures = new float?[TemperatureOffsets.Length];
        }
    }

    public static EcioPortGigabyteController TryCreate()
    {
        IT879xEcioPort port = new(EcioRegisterPort, EcioValuePort);

        // Check compatibility by querying its version.
        if (!port.ReadByte(ControllerFanControlArea + ControllerVersionOffset, out byte majorVersion) || majorVersion != 1)
            return null;

        return new EcioPortGigabyteController(port);
    }

    public bool Enable(bool enabled)
    {
        ushort offset = ControllerFanControlArea + ControllerEnableRegister;

        if (!_port.ReadByte(offset, out byte bCurrent))
            return false;

        bool current = Convert.ToBoolean(bCurrent);

        _initialState ??= current;

        if (current != enabled)
        {
            if (!_port.WriteByte(offset, (byte)(enabled ? 1 : 0)))
                return false;

            // Allow the system to catch up.
            Thread.Sleep(400);
        }

        return true;
    }

    public void Restore()
    {
        if (_initialState.HasValue)
            Enable(_initialState.Value);
    }

    public byte GetControl(int index)
    {
        if (index < 0 || index >= ControlOffsets.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        ushort offset = (ushort)(ExtraControllerFanControlArea + ControlOffsets[index]);
        if (!_port.ReadByte(offset, out byte value))
            return 0xFF;

        return value;
    }

    public void SetControl(int index, byte value)
    {
        if (index < 0 || index >= ControlOffsets.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        ushort offset = (ushort)(ExtraControllerFanControlArea + ControlOffsets[index]);
        _port.WriteByte(offset, value);
    }

    public void Update()
    {
        for (int i = 0; i < TemperatureOffsets.Length; i++)
        {
            ushort offset = (ushort)(ExtraControllerFanControlArea + TemperatureOffsets[i]);
            if (!_port.ReadByte(offset, out byte temperature))
                continue;
            ExtraTemperatures[i] = temperature;
        }

        for (int i = 0; i < FanOffsets.Length; i++)
        {
            ushort offset = (ushort)(ExtraControllerFanControlArea + FanOffsets[i]);
            if (!_port.ReadWord(offset, out ushort fan))
                continue;
            ExtraFans[i] = fan;
        }

        for (int i = 0; i < ControlOffsets.Length; i++)
        {
            ushort offset = (ushort)(ExtraControllerFanControlArea + ControlOffsets[i]);
            if (!_port.ReadByte(offset, out byte control))
                continue;
            ExtraControls[i] = (float)Math.Round(control * 100.0f / 0xFF);
        }
    }

    public float?[] ExtraControls { get; } = Array.Empty<float?>();

    public float?[] ExtraFans { get; } = Array.Empty<float?>();

    public float?[] ExtraTemperatures { get; } = Array.Empty<float?>();

    public string GetReport()
    {
        StringBuilder r = new();

        r.Append("Using ECIO port (0x");
        r.Append(_port.RegisterPort.ToString("X", CultureInfo.InvariantCulture));
        r.Append(", 0x");
        r.Append(_port.ValuePort.ToString("X", CultureInfo.InvariantCulture));
        r.AppendLine(")");

        return r.ToString();
    }
}
