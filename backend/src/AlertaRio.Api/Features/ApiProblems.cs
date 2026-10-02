namespace AlertaRio.Api.Features;

internal static class ApiProblems
{
    public static IResult Create(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            });
}
