using System;
using System.Text.RegularExpressions;
using System.Linq;
using PostCodeSerialMonitor.Models;
using Microsoft.Extensions.Logging;

namespace PostCodeSerialMonitor.Services;
public class SerialLineDecoder
{
    private readonly MetaDefinitionService _metaDefinitionService;
    private readonly ILogger<SerialLineDecoder> _logger;
    private static readonly Regex regex = new Regex(@"^(SMC|SP|CPU|OS)\s+?\((\d)\)\s?\:\s?([x0-9a-fA-F]{6})");

    public SerialLineDecoder(MetaDefinitionService metaDefinitionService, ILogger<SerialLineDecoder> logger)
    {
        _metaDefinitionService = metaDefinitionService ?? throw new ArgumentNullException(nameof(metaDefinitionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DecodedCode? DecodeLine(string inputLine, ConsoleType consoleType)
    {
        var match = regex.Match(inputLine);
        if (!match.Success)
        {
            _logger.LogDebug(Assets.Resources.DecoderIgnoringLine, inputLine);
            return null;
        }

        var codeFlavorStr = match.Groups[1].Value;
        var indexStr = match.Groups[2].Value;
        var codeStr = match.Groups[3].Value;

        var flavor = CodeFlavor.UNKNOWN;
        if (codeFlavorStr == "SMC")
            flavor = CodeFlavor.SMC;
        else if (codeFlavorStr == "SP")
            flavor = CodeFlavor.SP;
        else if (codeFlavorStr == "CPU")
            flavor = CodeFlavor.CPU;
        else if (codeFlavorStr == "OS")
            flavor = CodeFlavor.OS;

        var index = int.Parse(indexStr);
        var code = Convert.ToInt32(codeStr.Substring(2), 16);

        var decoded = new DecodedCode()
        {
            Flavor = flavor,
            Index = index,
            Code = code
        };

        // Until we have proper names for the E errors, bail out here early.
        if (flavor == CodeFlavor.OS && index == 1) {
            decoded.SeverityLevel = CodeSeverity.Error;
            decoded.Name = $"OS_ERROR_E{code}";
            decoded.Description = Assets.Resources.UemOsError;
            return decoded;
        }

        // First: Try to find distinct code
        var postCode = _metaDefinitionService.PostCodes.FirstOrDefault(x => 
            x.Code == code && 
            x.CodeFlavor == flavor && 
            !x.Bitmask.HasValue &&
            (x.ConsoleType.Contains(consoleType) || x.ConsoleType.First() == ConsoleType.ALL));
        
        if (postCode != null)
        {
            decoded.SeverityLevel = postCode.IsError ? CodeSeverity.Error : CodeSeverity.Info;
            decoded.Name = $"{flavor}_{postCode.Name}";
            decoded.Description = postCode.Description;
            return decoded;
        }

        // Second: Try to find error bitmask
        var postCodes = _metaDefinitionService.PostCodes.Where(x => 
            x.CodeFlavor == flavor &&
            x.Bitmask.HasValue &&
            (x.ConsoleType.Contains(consoleType) || x.ConsoleType.First() == ConsoleType.ALL) &&
            (code & x.Bitmask.Value) == x.Code
        ).ToList();
        
        if (postCodes != null && postCodes.Count > 0)
        {
            // Order matches from MSB to LSB, left to right and join the respective names with underscores
            var orderedMatches = postCodes
                .OrderByDescending(x => x.Bitmask!.Value)
                .ToList();

            decoded.SeverityLevel = postCodes.Any(x => x.IsError) ? CodeSeverity.Error : CodeSeverity.Info;
            decoded.Name = $"{flavor}_" + string.Join("_", orderedMatches.Select(x => x.Name));
            decoded.Description = string.Join("", orderedMatches.Select(x => x.Description));
            return decoded;
        }

        // Third: Try to match with more generic ErrorMasks
        var errorMask = _metaDefinitionService.ErrorMasks.FirstOrDefault(x => 
            x.CodeFlavor == flavor &&
            (x.ConsoleType.Contains(consoleType) || x.ConsoleType.First() == ConsoleType.ALL) &&
            (code & x.Bitmask) == x.Code
        );

        if (errorMask != null)
        {
            decoded.SeverityLevel = CodeSeverity.Error;
            decoded.Name = $"{flavor}_{errorMask.Name}_{code:X4}";
            decoded.Description = errorMask.Description;
            return decoded;
        }

        // Return without further metadata
        return decoded;
    }
}