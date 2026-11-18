using TruLoad.Backend.Configuration;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Infrastructure.Storage;

/// <summary>
/// Serves /media/{path} from the private R2 bucket (key truload/media/{path}) when object
/// storage is configured. It sits right after the local /media static files, so a file still on
/// local disk wins and everything else is streamed from R2. Branding images are shown on the
/// login page, so like the static files before it this needs no sign-in; the bucket itself
/// stays private.
/// </summary>
public static class ObjectStorageMediaMiddleware
{
    private static readonly PathString MediaPath = new("/media");

    public static IApplicationBuilder UseObjectStorageMedia(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) ||
                !request.Path.StartsWithSegments(MediaPath, out var rest) ||
                !rest.HasValue)
            {
                await next();
                return;
            }

            var storage = context.RequestServices.GetRequiredService<IObjectStorage>();
            var relative = rest.Value!.TrimStart('/');
            if (!storage.IsConfigured || relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal))
            {
                await next();
                return;
            }

            StoredObject? obj;
            try
            {
                obj = await storage.GetAsync(ObjectStorageOptions.MediaKey(relative), context.RequestAborted);
            }
            catch (Exception ex)
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(ObjectStorageMediaMiddleware))
                    .LogWarning(ex, "Reading media {Path} from object storage failed", relative);
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            if (obj == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await using (obj.Content)
            {
                context.Response.ContentType = obj.ContentType;
                if (obj.Length.HasValue) context.Response.ContentLength = obj.Length;
                if (!string.IsNullOrEmpty(obj.ETag)) context.Response.Headers.ETag = obj.ETag;
                // File names are random and never reused, so the content behind a URL never changes.
                context.Response.Headers.CacheControl = "public, max-age=86400";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                if (HttpMethods.IsGet(request.Method))
                {
                    await obj.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
                }
            }
        });
    }
}
