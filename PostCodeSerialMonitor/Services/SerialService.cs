using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PostCodeSerialMonitor.Services;
public class SerialService : IDisposable
{
    private readonly ILogger<SerialService> _logger;
    private ISerialPort? _serialPort;
    private CancellationTokenSource? _readCts;
    public event Action<string>? DataReceived;
    public event Action? Disconnected;
    public event Action? DeviceStateChanged;
    public event Action? DeviceConfigChanged;

    public string FirmwareVersion { get; private set; } = string.Empty;
    public string BuildDate { get; private set; } = string.Empty;

    public bool MirrorDisplay { get; private set; }
    public bool PortraitMode { get; private set; }
    public bool PrintTimestamps { get; private set; }
    public bool PrintColors { get; private set; }

    public SerialService(ILogger<SerialService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IEnumerable<string> GetPortNames()
    {
        return SerialPort.GetPortNames();
    }

    public bool IsOpen => _serialPort?.IsOpen ?? false;

    public async Task<bool> GoToREPL()
    {
        if (!IsOpen)
            return false;

        // Reset sequence
        for (int attempt = 0; attempt < 2; attempt++)
        {
            _serialPort?.WriteLine("\x03"); // CTRL+C
            _serialPort?.WriteLine(""); // ENTER

            var buf = await ReadUntilEnd();
            if (buf.EndsWith(">> "))
                return true;
        }

        return false;
    }

    public async Task ConnectAsync(string portName, int baudRate = 115200)
    {
        if (_serialPort != null && _serialPort.IsOpen)
            _serialPort.Close();

        _serialPort = new SerialPortWrapper(portName, baudRate);
        _serialPort.Open();
        _readCts = new CancellationTokenSource();

        var success = await GoToREPL();
        if (!success)
        {
            Disconnect();
            throw new Exception(Assets.Resources.FailedEnterRepl);
        }

        // Get version info
        _serialPort.WriteLine("version");
        Thread.Sleep(100);
        success = await ParseVersionInfo();
        if (!success)
        {
            Disconnect();
            throw new Exception(Assets.Resources.FailedFwVersion);}

        // Get config state
        _serialPort.WriteLine("config");
        Thread.Sleep(100);
        success = await ParseConfigState();
        if (!success)
        {
            Disconnect();
            throw new Exception(Assets.Resources.FailedParseConfig);
        }

        if (PrintColors)
        {
            _serialPort.WriteLine("colors");
        }

        // Start post command
        _serialPort.WriteLine("post");

        // Start reading loop
        _ = Task.Run(() => ReadLoop(_readCts.Token));
    }

    private async Task<string> ReadUntilEnd()
    {
        return await Task.Run(() =>
        {
            var output = "";

            while (_serialPort!.BytesToRead > 0)
            {
                output += _serialPort!.ReadChar();
            }
            _logger.LogTrace(
                "ReadUntilEnd:\n###START###\n{output}\n###END### Len: {outputLength} bytes\n",
                output,
                output.Length
            );
            return output;
        });
    }

    private async Task<bool> ParseVersionInfo()
    {
        var res = await ReadUntilEnd();
        if (!res.Contains("FW: "))
        {
            _logger.LogError(Assets.Resources.FailedGetVersion);
            return false;
        }

        foreach (var line in res.Split("\r\n"))
        {
            if (line.StartsWith("FW: "))
            {
                var parts = line.Substring(4).Split(' ');
                if (parts.Length >= 2)
                {
                    FirmwareVersion = parts[0];
                    BuildDate = parts[1];
                    DeviceStateChanged?.Invoke();
                }
            }
        }
        return true;
    }

    private async Task<bool> ParseConfigState()
    {
        var res = await ReadUntilEnd();
        if (!res.Contains("Display mirrored:"))
        {
            _logger.LogError(Assets.Resources.FailedGetState);
            return false;
        }

        foreach (var line in res.Split("\r\n"))
        {
            if (line.Contains("Display mirrored:"))
                MirrorDisplay = line.Contains("ON") ? true : false;
            else if (line.Contains("Disp rotation portrait:"))
                PortraitMode = line.Contains("ON") ? true : false;
            else if (line.Contains("Print timestamps:"))
                PrintTimestamps = line.Contains("ON") ? true : false;
            else if (line.Contains("Print colors:"))
                PrintColors = line.Contains("ON");
        }

        DeviceConfigChanged?.Invoke();
        return true;
    }

    public void Disconnect()
    {
        if (_serialPort != null)
        {
            _readCts?.Cancel();
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
            Disconnected?.Invoke();
        }
        MirrorDisplay = false;
        PortraitMode = false;
        PrintTimestamps = false;
        PrintColors = false;
    }

    private void ReadLoop(CancellationToken token)
    {
        try
        {
            var line = "";
            while (!token.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
            {
                line += _serialPort.ReadChar();

                if (line.EndsWith("\r\n"))
                {
                    DataReceived?.Invoke(line);
                    line = "";
                }
            }
        }
        catch (Exception)
        {
            Disconnected?.Invoke();
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}