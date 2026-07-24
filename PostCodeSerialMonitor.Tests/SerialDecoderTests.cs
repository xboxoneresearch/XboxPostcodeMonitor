using System.Collections;
using PostCodeSerialMonitor.Models;
using PostCodeSerialMonitor.Services;
using System.IO;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Moq;

namespace PostCodeSerialMonitor.Tests;
public class TestDataGenerator : IEnumerable<object[]>
{
    private readonly List<object[]> _data = new List<object[]>
    {
        new object[] {"CPU: 0x14ff (+0.000 mS)", new DecodedCode(){
            Flavor = CodeFlavor.CPU,
            Code = 0x14ff
        }},
        new object[] {"CPU: 0x14ff", new DecodedCode(){
            Flavor = CodeFlavor.CPU,
            Code = 0x14ff
        }},
        new object[] {"SP : 0x75 (+4252.064 mS)", new DecodedCode(){
            Flavor = CodeFlavor.SP,
            Code = 0x0075
        }},
        new object[] {"SP : 0x75", new DecodedCode(){
            Flavor = CodeFlavor.SP,
            Code = 0x0075
        }}
    };

    public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class SerialDecoderTests
{
    private readonly SerialLineDecoder _decoder;
    private readonly string _testDataPath;

    public SerialDecoderTests()
    {
        _testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "postcodes.csv");
        var csvContent = File.ReadAllText(_testDataPath);
        
        // Create configuration
        var config = new AppConfiguration();
        var optionsMonitor = new Mock<IOptionsMonitor<AppConfiguration>>();
        optionsMonitor.Setup(m => m.CurrentValue).Returns(config);

        // Create logger factory
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var loggerMeta = new Mock<ILogger<MetaDefinitionService>>().Object;
        var loggerDecoder = new Mock<ILogger<SerialLineDecoder>>().Object;
        
        // Create configuration service
        var configService = new ConfigurationService(
            optionsMonitor.Object,
            loggerFactory.CreateLogger<ConfigurationService>(),
            Path.GetDirectoryName(_testDataPath) ?? "."
        );

        // Create JSON options
        var jsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        // Create MetaUpdateService
        var metaUpdateService = new MetaUpdateService(
            configService,
            jsonOptions,
            loggerFactory.CreateLogger<MetaUpdateService>()
        );

        // Create MetaDefinitionService
        var metaDefinitionService = new MetaDefinitionService(configService, metaUpdateService, loggerMeta);
        metaDefinitionService.RefreshMetaDefinitionsAsync().GetAwaiter().GetResult();

        _decoder = new SerialLineDecoder(metaDefinitionService, loggerDecoder);
    }

    [Theory]
    [ClassData(typeof(TestDataGenerator))]
    public void TestDecoding(string input, DecodedCode expected)
    {
        var result = _decoder.DecodeLine(input, ConsoleType.XboxOnePhat);
        Assert.NotNull(result);
        Assert.Equal(expected.Flavor, result.Flavor);
        Assert.Equal(expected.Code, result.Code);
    }
}