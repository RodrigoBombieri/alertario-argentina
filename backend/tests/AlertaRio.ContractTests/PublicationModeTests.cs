using AlertaRio.Api;
using AlertaRio.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlertaRio.ContractTests;

public sealed class PublicationModeTests
{
    [Fact]
    public void Published_mode_rejects_cold_start_demo()
    {
        Assert.Throws<InvalidOperationException>(() => ApiHost.Build(
            new WebApplicationOptions
            {
                EnvironmentName = "Production",
                ApplicationName = typeof(ApiHost).Assembly.GetName().Name
            }, builder =>
            {
                builder.Configuration["PublishedData:Enabled"] = "true";
                builder.Configuration["ColdStart:Enabled"] = "true";
            }));
    }

    [Fact]
    public void GeoRef_import_requires_explicit_rights_selection()
    {
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["GeoRefIngestion:SourceId"] = Guid.NewGuid().ToString("D"),
                ["GeoRefIngestion:RightsDecisionId"] = "fixture-rights"
            }).Build();
        Assert.Throws<InvalidOperationException>(() =>
            GeoRefPollingOptions.Read(disabled));

        var sourceId = Guid.NewGuid();
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["GeoRefIngestion:ActivationAcknowledged"] = "true",
                ["GeoRefIngestion:SourceId"] = sourceId.ToString("D"),
                ["GeoRefIngestion:RightsDecisionId"] = "fixture-rights",
                ["GeoRefIngestion:PollIntervalDays"] = "7"
            }).Build();
        var options = GeoRefPollingOptions.Read(enabled);
        Assert.Equal(sourceId, options.SourceId);
        Assert.Equal(TimeSpan.FromDays(7), options.PollInterval);
    }
}
