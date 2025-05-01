using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using synk;




bool cliOK = GlobalConfig.CommandLineParse(args);

var builder = WebApplication.CreateBuilder(args);
if (!cliOK)
{
    DBg.d(LogLevel.Critical, "Command line parsing failed. Exiting.");
    Environment.Exit(1);
}


DBg.d(LogLevel.Information, $"synk:{GlobalConfig.bldVersion}");


builder.WebHost.UseUrls($"http://{GlobalConfig.Bind}:{GlobalConfig.Port}");

var app = builder.Build();
// this configures the middleware to respect the X-Forwarded-For and X-Forwarded-Proto headers
// that are set by any reverse proxy server (nginx, apache, etc.)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseRouting();


app.MapGet("/about", (HttpContext httpContext) =>
{
    string fn = "/about"; DBg.d(LogLevel.Trace, fn);
    StringBuilder sb = new StringBuilder();
    GlobalStatic.GenerateHTMLHead(sb, "About Synk");
    sb.AppendLine("<p><code>synk</code> is a simple, single binary self-hosted webservice that allows anonymous key-based data storage and retrieval.</p>");
    sb.AppendLine("<p>To use it, make a <code>PUT</code> request to:</p>");
    sb.AppendLine($"<pre>{GlobalConfig.Hostname}/blob/{{key}}</pre>");
    sb.AppendLine("<p>To get the same data back, make a <code>GET</code> request to the same URL.</p>");
    sb.AppendLine("<p>For example, using <code>curl</code>:</p>");
    sb.AppendLine($"<pre><code>curl -X PUT \"http://{GlobalConfig.Hostname}/blob/abc123\" --data-binary @excellent.meme.png</code></pre>");
    sb.AppendLine("<p>uploads a file to key <code>abc123</code> (obviously you'd want something better than that). And to retrieve:</p>");
    sb.AppendLine($"<pre><code>curl \"http://{GlobalConfig.Hostname}/blob/abc123\" --output &lt;FILE&gt;</code></pre>");
    sb.AppendLine("<p>It's up to you to know <em>what</em> data is stored at that key - no metadata about the payload is provided when you upload, so no metadata about it is available when you retrieve it.</p>");
    sb.AppendLine("<p>If you <code>GET</code> a key using a web browser, it will likely just write an extension-less <code>{key}</code> named file to your Downloads folder.</p>");
    sb.AppendLine("<ul>");
    sb.AppendLine("<li>If you <code>PUT</code> using an existing key, you overwrite the data.</li>");
    sb.AppendLine($"<li>If the size of what you <code>PUT</code> is too big, you get <code>HTTP 413 Payload Too Large</code> - the size of the \"synkstore\" is configurable by the owner (here it is {GlobalStatic.PrettySize(GlobalConfig.maxSynkStoreSize)}).</li>");
    sb.AppendLine("<li><code>{key}</code>s can be whatever you like up to 512 bytes long (good enough for most crypto keys).</li>");
    sb.AppendLine("<ul>");
    sb.AppendLine("<li>But as a good practice, it should be at least 16 bytes long.</li>");
    sb.AppendLine("<li>As a convenience, there is <a href=\"/key\">/key</a> which generates GUIDs.</li>");
    sb.AppendLine("</ul>");
    sb.AppendLine("<li>Data is retrieved exactly as it is stored - if you <code>GET</code> a valid key, you <code>GET</code> the data.</li>");
    sb.AppendLine("<li>But YOU can always encrypt the data before you store it with the <em>key</em>.</li>");
    sb.AppendLine("</ul>");
    sb.AppendLine($"<p>This <code>synk</code> instance is provided by {GlobalConfig.siteInformation}</p>");
    GlobalStatic.GeneratePageFooter(sb);
    return Results.Text(sb.ToString(), "text/html");
});




//redirect pathless requests to to the about page
app.MapGet("/", (HttpContext httpContext) =>
{
    string fn = "/"; DBg.d(LogLevel.Trace, fn);
    return Results.Redirect("/about");
});

app.MapGet("/key", (HttpContext httpContext) =>
{
    string fn = "/key"; DBg.d(LogLevel.Trace, fn);
    Guid id = Guid.NewGuid();
    string key = id.ToString();
    return Results.Text(key, "text/plain");
}).AllowAnonymous();

// endpoint that adds a blobkey and stores/updates its data
app.MapPut("/blob/{key}", async (HttpContext httpContext, string key) =>
{
    string fn = "/blob"; DBg.d(LogLevel.Trace, fn);

    if (blobController.IsUrlEncoded(key))
    {
        // decode the key
        key = blobController.UrlDecode(key);
    }

    // we allow keys a max length of 512 characters
    if (key.Length > 512)
    {
        DBg.d(LogLevel.Error, $"Blob key {key} is too long");
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }
    long payloadSize = httpContext.Request.ContentLength ?? 0;
    // if there is no Put data, return a 400
    if (payloadSize == null || payloadSize == 0)
    {
        DBg.d(LogLevel.Error, $"No Data provided to {key}..");
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    // if the content length is > however much free space in our synkstore
    // return http 413
    long currentStoreSize = GlobalStatic.synkStoreSize();
    if(currentStoreSize + payloadSize > GlobalConfig.maxSynkStoreSize)
    {
        DBg.d(LogLevel.Error, $"Blob store size {GlobalStatic.PrettySize(currentStoreSize)} (existing) + {GlobalStatic.PrettySize(payloadSize)} (new) is larger than MAX {GlobalStatic.PrettySize(GlobalConfig.maxSynkStoreSize)}");
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    // get the blob data from the request body
    using (var ms = new MemoryStream())
    {
        await httpContext.Request.Body.CopyToAsync(ms);
        byte[] data = ms.ToArray();
        // write the blob file
        try
        {
            blobController.WriteBlobFile(key, data);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Exception occurred: {ex}");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }


}).AllowAnonymous();

// endpoint that returns the blob data for the provided key
app.MapGet("/blob/{key}", (HttpContext httpContext, string key) =>
{
    string fn = "/blob"; DBg.d(LogLevel.Trace, fn);

    if (blobController.IsUrlEncoded(key))
    {
        // decode the key
        key = blobController.UrlDecode(key);
    }

    // we allow keys a max length of 512 characters
    if (key.Length > 512)
    {
        DBg.d(LogLevel.Error, $"Blob key {key} is too long");
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    // read the blob file and return the data
    byte[]? data = blobController.ReadBlobFile(key);
    if (data == null)
    {
        return Results.NoContent();
    }
    else
    {
        return Results.File(data, "application/octet-stream");
    }
}).AllowAnonymous();


// Mutex to ensure only one of us is running

bool createdNew;
using (var mutex = new Mutex(true, GlobalStatic.applicationName, out createdNew))
{
    if (createdNew)
    {
        // initial load any existing blobkeys
        blobController.LoadBlobKeys();
        blobController.VerifyBlobKeyFiles();
        app.Run();
    }
    else
    {
        Console.WriteLine("Another instance of the application is already running.");
    }
}







