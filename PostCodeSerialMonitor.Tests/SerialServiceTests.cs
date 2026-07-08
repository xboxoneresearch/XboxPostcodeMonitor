using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PostCodeSerialMonitor.Models;
using PostCodeSerialMonitor.Services;
using Xunit;

namespace PostCodeSerialMonitor.Tests;

/*
public class SerialServiceTests
{
    private readonly Mock<ISerialPort> _mockSerialPort;
    private readonly SerialService _serialService;

    public SerialServiceTests()
    {
        var logger = new Mock<ILogger<SerialService>>();
        _mockSerialPort = new Mock<ISerialPort>();
        _serialService = new SerialService(logger.Object);
    }

    [Fact]
    public async Task Connect_ShouldSendResetSequence()
    {
        // Arrange
        var outputSequence = new[]
        {
            ">>version",
            "FW: v0.2.1 20240521",
            ">>config",
            "Notice: Showing config",
            "Display mirrored:       ON",
            "Disp rotation portrait: OFF",
            "Print timestamps:       ON",
            "Print colors:           ON",
            ">>"
        };

        var currentLine = 0;
        _mockSerialPort.Setup(x => x.ReadLine())
            .Returns(() => outputSequence[currentLine++]);

        // Act
        await _serialService.ConnectAsync("COM1");

        // Assert
        _mockSerialPort.Verify(x => x.Write("\x03"), Times.Once); // CTRL+C
        _mockSerialPort.Verify(x => x.Write("\r\n"), Times.Once); // ENTER
        _mockSerialPort.Verify(x => x.Write("version\r\n"), Times.Once);
        _mockSerialPort.Verify(x => x.Write("config\r\n"), Times.Once);
        _mockSerialPort.Verify(x => x.Write("post\r\n"), Times.Once);
    }

    [Fact]
    public async Task Connect_ShouldParseVersionInfo()
    {
        // Arrange
        var outputSequence = new[]
        {
            ">>",
            "FW: v0.2.1 20240521",
            ">>",
            "Notice: Showing config",
            "Display mirrored:       ON",
            "Disp rotation portrait: OFF",
            "Print timestamps:       ON",
            "Print colors:           ON",
            ">>"
        };

        var currentLine = 0;
        _mockSerialPort.Setup(x => x.ReadLine())
            .Returns(() => outputSequence[currentLine++]);

        // Act
        await _serialService.ConnectAsync("COM1");

        // Assert
        Assert.Equal("v0.2.1", _serialService.FirmwareVersion);
        Assert.Equal("20240521", _serialService.BuildDate);
    }

    [Fact]
    public async Task Connect_ShouldParseConfigState()
    {
        // Arrange
        var outputSequence = new[]
        {
            ">>",
            "FW: v0.2.1 20240521",
            "Notice: Showing config",
            "Display mirrored:       ON",
            "Disp rotation portrait: OFF",
            "Print timestamps:       ON",
            "Print colors:           ON",
            ">>"
        };

        var currentLine = 0;
        _mockSerialPort.Setup(x => x.ReadLine())
            .Returns(() => outputSequence[currentLine++]);

        // Act
        await _serialService.ConnectAsync("COM1");

        // Assert
        Assert.True(_serialService.MirrorDisplay);
        Assert.False(_serialService.PortraitMode);
        Assert.True(_serialService.PrintTimestamps);
    }

    [Fact]
    public async Task Connect_ShouldRetryOnFailedReset()
    {
        // Arrange
        var outputSequence = new[]
        {
            "some unexpected output",
            ">>",
            "FW: v0.2.1 20240521",
            "Notice: Showing config",
            "Display mirrored:       ON",
            "Disp rotation portrait: OFF",
            "Print timestamps:       ON",
            "Print colors:           ON",
            ">>"
        };

        var currentLine = 0;
        _mockSerialPort.Setup(x => x.ReadLine())
            .Returns(() => outputSequence[currentLine++]);

        // Act
        await _serialService.ConnectAsync("COM1");

        // Assert
        _mockSerialPort.Verify(x => x.Write("\x03"), Times.Exactly(2)); // CTRL+C twice
        _mockSerialPort.Verify(x => x.Write("\r\n"), Times.Exactly(2)); // ENTER twice
    }
}
*/

public class SerialServiceDisconnectTests
{
    [Fact]
    public async Task Disconnect_WhileReadLoopIsBlockedOnRead_FiresDisconnectedExactlyOnce()
    {
        // Arrange
        var logger = new Mock<ILogger<SerialService>>();
        var service = new SerialService(logger.Object);

        var readerBlocked = new ManualResetEventSlim(false);   // ReadLoop has entered ReadChar()
        var releaseReader = new ManualResetEventSlim(false);   // Close() happened, ReadChar() may throw

        var mockPort = new Mock<ISerialPort>();
        mockPort.SetupGet(p => p.IsOpen).Returns(true);
        mockPort.Setup(p => p.ReadChar()).Returns(() =>
        {
            readerBlocked.Set();
            releaseReader.Wait(TimeSpan.FromSeconds(5));
            throw new IOException("Port closed while a read was pending");
        });
        mockPort.Setup(p => p.Close()).Callback(() => releaseReader.Set());

        service._serialPort = mockPort.Object;
        service._readCts = new CancellationTokenSource();

        var disconnectedCount = 0;
        service.Disconnected += () => Interlocked.Increment(ref disconnectedCount);

        var readLoopTask = Task.Run(() => service.ReadLoop(service._readCts.Token));

        // Ensure ReadLoop is genuinely blocked inside ReadChar() before disconnecting,
        // to reproduce "close happens while a read is pending" deterministically.
        Assert.True(readerBlocked.Wait(TimeSpan.FromSeconds(2)), "ReadLoop never reached ReadChar()");

        // Act: single call, same as MainWindowViewModel.ToggleConnectionAsync does
        service.Disconnect();

        // Wait for the background ReadLoop thread's catch block to finish too.
        await readLoopTask;

        // Assert
        Assert.Equal(1, disconnectedCount);
    }
}
