using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using PostCodeSerialMonitor.Models;
using Microsoft.Extensions.Logging;

namespace PostCodeSerialMonitor.Services;
public class MetaUpdateService
{
    private readonly ConfigurationService _configurationService;
    private readonly JsonSerializerOptions _jsonSerializeOptions;
    private readonly string _localPath;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MetaUpdateService> _logger;
    private const string META_FILENAME = "meta.json";
    private MetaDefinition? _currentMeta;

    public string LocalMetaPath => Path.Combine(_localPath, META_FILENAME);
    public IReadOnlyList<MetaEntry> LocalMetaEntries => _currentMeta?.Items ?? Array.Empty<MetaEntry>();
    public DateTime? LastUpdateTime => _currentMeta?.Updated;
    public AppConfiguration Config => _configurationService.Config;

    public MetaUpdateService(
        ConfigurationService configurationService, 
        JsonSerializerOptions jsonOptions, 
        ILogger<MetaUpdateService> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _jsonSerializeOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        _localPath = _configurationService.Config.MetaStoragePath
            ?? throw new ArgumentNullException(nameof(_configurationService.Config.MetaStoragePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient();
    }

    public async Task<bool> TryLoadLocalDefinition()
    {
        var localMeta = await GetLocalMetaDefinitionAsync();
        if (localMeta == null)
            return false;

        _currentMeta = localMeta;
        return true;
    }

    public async Task<bool> CheckForMetaDefinitionUpdatesAsync()
    {
        var localMeta = await GetLocalMetaDefinitionAsync();
        var remoteMeta = await GetRemoteMetaDefinitionAsync();
        
        if (localMeta == null || remoteMeta == null)
        {
            // Update required
            return true;
        }

        return remoteMeta > localMeta;
    }

    public async Task UpdateMetaDefinitionAsync()
    {
        if (!Config.CheckForCodeUpdates)
            return;

        var metaContent = await GetRemoteMetaDefinitionAsync();

        if (metaContent == null)
            return;

        var metaContentStr = JsonSerializer.Serialize(metaContent, _jsonSerializeOptions);
        _logger.LogDebug(Assets.Resources.MetaContent, metaContentStr);

        // Ensure directory exists
        Directory.CreateDirectory(_localPath);
        
        // Save the new meta definition
        await File.WriteAllTextAsync(LocalMetaPath, metaContentStr);

        // Parse and store the current meta definition
        _currentMeta = metaContent;

        // Download all files specified in the meta definition
        await DownloadMetaFilesAsync();
    }

    /*
    * Downloads the entries (CSV) from the MetaDefinition
    * They are located at the same path-level as meta.json
    */
    private async Task DownloadMetaFilesAsync()
    {
        if (_currentMeta == null || _currentMeta.Items == null)
            return;

        // Get the base URL by removing the filename from the meta definition URL
        var baseUrl = Config.CodesMetaBaseUrl;

        foreach (var item in _currentMeta.Items)
        {
            var fileUrl = baseUrl + item.Path;
            var localFilePath = Path.Combine(_localPath, item.Path);

            // Ensure the directory exists
            Directory.CreateDirectory(_localPath);

            try
            {
                var content = await _httpClient.GetStringAsync(fileUrl);
                await File.WriteAllTextAsync(localFilePath, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Assets.Resources.FailedDownloadMetaEntry, fileUrl);
            }
        }
    }

    private async Task<MetaDefinition?> GetLocalMetaDefinitionAsync()
    {
        if (!File.Exists(LocalMetaPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(LocalMetaPath);
            var res = JsonSerializer.Deserialize<MetaDefinition>(json, _jsonSerializeOptions);
            _currentMeta = res;
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, Assets.Resources.FailedDeserializingMetaDefinition);
            return null;
        }
    }

    private async Task<MetaDefinition?> GetRemoteMetaDefinitionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(Config.MetaJsonUrl);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MetaDefinition>(json, _jsonSerializeOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, Assets.Resources.FailedDownloadMetaDefinition, Config.MetaJsonUrl);
            return null;
        }
    }
}