// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Threading;

namespace LibreHardwareMonitor.Hardware.Motherboard.Lpc;

internal class IT879xEcioPort
{
    public IT879xEcioPort(ushort registerPort, ushort valuePort)
    {
        RegisterPort = registerPort;
        ValuePort = valuePort;
    }

    public ushort RegisterPort { get; }

    public ushort ValuePort { get; }

    public bool ReadByte(ushort offset, out byte value)
    {
        value = 0xFF;

        if (!WriteToRegister(0xB0) ||
            !WriteToValue((byte)((offset >> 8) & 0xFF)) ||
            !WriteToValue((byte)(offset & 0xFF)))
            return false;

        return ReadFromValue(out value);
    }

    public bool WriteByte(ushort offset, byte value)
    {
        if (!WriteToRegister(0xB1) ||
            !WriteToValue((byte)((offset >> 8) & 0xFF)) ||
            !WriteToValue((byte)(offset & 0xFF)))
            return false;

        return WriteToValue(value);
    }

    public bool ReadWord(ushort offset, out ushort value)
    {
        value = 0xFFFF;

        if (!ReadByte(offset, out byte b1) ||
            !ReadByte((ushort)(offset + 1), out byte b2))
            return false;

        value = (ushort)(b1 | (b2 << 8));
        return true;
    }

    public bool WriteWord(ushort offset, ushort value)
    {
        return WriteByte(offset, (byte)(value & 0xFF)) &&
            WriteByte((ushort)(offset + 1), (byte)((value >> 8) & 0xFF));
    }

    private bool WriteToRegister(byte value)
    {
        if (!WaitIBE())
            return false;
        Ring0.WriteIoPort(RegisterPort, value);
        return WaitIBE();
    }

    private bool WriteToValue(byte value)
    {
        if (!WaitIBE())
            return false;
        Ring0.WriteIoPort(ValuePort, value);
        return WaitIBE();
    }

    private bool ReadFromValue(out byte value)
    {
        value = 0xFF;
        if (!WaitOBF())
            return false;
        value = Ring0.ReadIoPort(ValuePort);
        return true;
    }

    private bool WaitIBE()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            while ((Ring0.ReadIoPort(RegisterPort) & 2) != 0)
            {
                if (stopwatch.ElapsedMilliseconds > WaitTimeout)
                    return false;

                Thread.Sleep(1);
            }
            return true;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private bool WaitOBF()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            while ((Ring0.ReadIoPort(RegisterPort) & 1) == 0)
            {
                if (stopwatch.ElapsedMilliseconds > WaitTimeout)
                    return false;

                Thread.Sleep(1);
            }
            return true;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private const long WaitTimeout = 1000L;
}
