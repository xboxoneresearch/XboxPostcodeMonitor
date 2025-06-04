using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using CsvHelper;
using CsvHelper.Configuration;
using PostCodeSerialMonitor.Models;


namespace PostCodeSerialMonitor.Services;
public static class CsvParsingService
{
    public static List<PostCodeDefinition> ParsePostCodes(string csvData)
    {
        return Parse<PostCodeDefinition>(csvData);
    }

    public static List<ErrorMaskDefinition> ParseErrorMasks(string csvData)
    {
        return Parse<ErrorMaskDefinition>(csvData);
    }

    public static List<OSErrorDefinition> ParseOSErrors(string csvData)
    {
        return Parse<OSErrorDefinition>(csvData);
    }

    private static List<T> Parse<T>(string csvData)
    {
        using var reader = new StringReader(csvData);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null
        });

        return csv.GetRecords<T>().ToList();
    }
}