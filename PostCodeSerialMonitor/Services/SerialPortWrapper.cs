using System.IO.Ports;

namespace PostCodeSerialMonitor.Services;
public class SerialPortWrapper : ISerialPort
{
    private readonly SerialPort _port;

    public SerialPortWrapper(string portName, int baudRate = 115200)
    {
        _port = new SerialPort(portName, baudRate)
        {
            Encoding = System.Text.Encoding.UTF8,
            NewLine = "\r\n",
            RtsEnable = true,
            DtrEnable = true,
        };
    }

    public bool IsOpen => _port.IsOpen;
    public int BytesToRead => _port.BytesToRead;

    public void Open() => _port.Open();
    public void Close() => _port.Close();
    public void Write(string text) => _port.Write(text);
    public void WriteLine(string text) => _port.WriteLine(text);
    public char ReadChar() => (char)_port.ReadChar();
    public string ReadLine() => _port.ReadLine();

    public void Dispose()
    {
        _port.Dispose();
    }
}