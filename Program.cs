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
  
    return Results.Text(GlobalStatic.staticAboutPage, "text/html");
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
    
    string guid = id.ToString();
    
    string threeword = GlobalStatic.ThreeWords();
    
    // generate a valid 512 bit key suitable for use as a private key
    string randomkey = GlobalStatic.RandomHexKey();

    return Results.Text($"{guid}\n{threeword}\n{randomkey}", "text/plain");
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
    if (payloadSize == 0)
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







