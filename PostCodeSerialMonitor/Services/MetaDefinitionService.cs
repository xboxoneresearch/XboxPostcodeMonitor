using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PostCodeSerialMonitor.Models;

namespace PostCodeSerialMonitor.Services;
public class MetaDefinitionService
{
    private readonly MetaUpdateService _metaUpdateService;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<MetaDefinitionService> _logger;

    private List<PostCodeDefinition> _postCodes = new();
    private List<ErrorMaskDefinition> _errorMasks = new();
    private List<OSErrorDefinition> _osErrors = new();

    public IReadOnlyList<PostCodeDefinition> PostCodes => _postCodes;
    public IReadOnlyList<ErrorMaskDefinition> ErrorMasks => _errorMasks;
    public IReadOnlyList<OSErrorDefinition> OSErrors => _osErrors;

    public MetaDefinitionService(ConfigurationService configurationService, MetaUpdateService metaUpdateService, ILogger<MetaDefinitionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _metaUpdateService = metaUpdateService ?? throw new ArgumentNullException(nameof(metaUpdateService));
    }

    public async Task RefreshMetaDefinitionsAsync()
    {
        // Clear existing definitions
        _postCodes.Clear();
        _errorMasks.Clear();
        _osErrors.Clear();

        // Get all meta entries
        var metaEntries = _metaUpdateService.LocalMetaEntries;

        foreach (var entry in metaEntries)
        {
            var filePath = Path.Combine(_configurationService.Config.MetaStoragePath, entry.Path);
            if (!File.Exists(filePath))
            {
                _logger.LogError(Assets.Resources.MetaFileNotExist, filePath);
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath);

            switch (entry.MetaType)
            {
                case MetaType.PostCodes:
                    _postCodes.AddRange(CsvParsingService.ParsePostCodes(content));
                    break;
                case MetaType.ErrorMasks:
                    _errorMasks.AddRange(CsvParsingService.ParseErrorMasks(content));
                    break;
                case MetaType.OSErrors:
                    _osErrors.AddRange(CsvParsingService.ParseOSErrors(content));
                    break;
                default:
                    _logger.LogError(Assets.Resources.UnexpectedMetaType, entry.MetaType);
                    break;
            }
        }
    }
}