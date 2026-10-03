namespace AlertaRio.Infrastructure.Providers;

// Use these clients when approved providers are wired into a production host.
public static class ProviderHttpClientFactory
{
    public static HttpClient CreateIna() => Create("https://alerta.ina.gob.ar/a5/");

    public static HttpClient CreateGeoRef() =>
        Create("https://apis.datos.gob.ar/georef/api/v2.0/");

    private static HttpClient Create(string baseAddress) => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    {
        BaseAddress = new Uri(baseAddress)
    };
}
