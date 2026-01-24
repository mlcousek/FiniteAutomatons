using FiniteAutomatons.Core.Utilities;
using Microsoft.Extensions.Primitives;
using System.Text;

namespace FiniteAutomatons.Middleware;

public class EpsilonNormalizationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context)
    {
        var req = context.Request;

        // Only care about POST/PUT/PATCH where a symbol may be sent
        if (HttpMethods.IsPost(req.Method) || HttpMethods.IsPut(req.Method) || HttpMethods.IsPatch(req.Method))
        {
            // Handle form posts (application/x-www-form-urlencoded, multipart/form-data)
            if (req.HasFormContentType)
            {
                var form = await req.ReadFormAsync();
                var dict = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in form)
                {
                    var values = kv.Value;
                    if (values.Count == 0)
                    {
                        dict[kv.Key] = values;
                        continue;
                    }

                    var outVals = new string[values.Count];
                    for (int i = 0; i < values.Count; i++)
                    {
                        outVals[i] = NormalizeEpsilon(values[i]);
                    }
                    dict[kv.Key] = new StringValues(outVals);
                }

                // Preserve files
                var newForm = new FormCollection(dict, form.Files);
                req.Form = newForm;
            }
            else if (!string.IsNullOrEmpty(req.ContentType) && req.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                // For JSON bodies do a simple string replacement of the unicode replacement char -> NUL
                req.EnableBuffering();
                using var sr = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var body = await sr.ReadToEndAsync();
                if (!string.IsNullOrEmpty(body) && body.Contains('\uFFFD'))
                {
                    var newBody = body.Replace('\uFFFD', '\0');
                    var bytes = Encoding.UTF8.GetBytes(newBody);
                    req.Body = new MemoryStream(bytes);
                    req.ContentLength = bytes.Length;
                }
                else
                {
                    req.Body.Position = 0;
                }
            }
        }

        await next(context);
    }

    private static string NormalizeEpsilon(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        if (input.Contains('\uFFFD'))
        {
            return input.Replace('\uFFFD', AutomatonSymbolHelper.EpsilonInternal);
        }
        return input;
    }
}
