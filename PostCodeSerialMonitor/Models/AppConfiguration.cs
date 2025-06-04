using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PostCodeSerialMonitor.Models;
public class AppConfiguration
{
    [Required]
    [Range(1, int.MaxValue)]
    public int FormatVersion { get; set; } = 1;

    public bool CheckForAppUpdates { get; set; } = true;
    public bool CheckForCodeUpdates { get; set; } = true;
    public bool CheckForFwUpdates { get; set; } = true;

    [Required]
    [Url]
    public Uri AppUpdateUrl { get; set; } = new Uri("https://example.com/todo");

    [Required]
    [Url]
    public Uri CodesMetaBaseUrl { get; set; } = new Uri("https://errors.xboxresearch.com");

    [Required]
    [Url]
    public Uri FwUpdateUrl { get; set; } = new Uri("https://example.com/todo");

    [Required]
    public string Language { get; set; } = "en-US";

    [NotMapped]
    public string MetaStoragePath => "meta";

    [NotMapped]
    public Uri MetaJsonUrl => new Uri(CodesMetaBaseUrl, "meta.json");
}
