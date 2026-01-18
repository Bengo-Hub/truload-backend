using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Configuration for HTTP response compression to reduce bandwidth and improve performance.
/// Compresses responses using Gzip and Brotli algorithms for better client experience.
/// </summary>
public static class ResponseCompressionConfiguration
{
    /// <summary>
    /// Configures response compression services with optimal settings.
    /// Uses Brotli (preferred) and Gzip compression with quality level 4 for balance of speed/compression.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddTruLoadResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Enable compression for both HTTP and HTTPS
            options.EnableForHttps = true;

            // Add compression providers in order of preference (Brotli > Gzip)
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            // MIME types to compress (JSON, HTML, XML, CSS, JavaScript, SVG, PDF)
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/xml",
                "text/html",
                "text/json",
                "text/plain",
                "text/xml",
                "application/pdf",
                "image/svg+xml",
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            });
        });

        // Configure Brotli compression (better compression than Gzip, supported by modern browsers)
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            // CompressionLevel.Optimal provides good balance between compression ratio and speed
            // Level 4 is recommended for dynamic content (faster than level 11, still good compression)
            options.Level = CompressionLevel.Optimal;
        });

        // Configure Gzip compression (fallback for older browsers)
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            // CompressionLevel.Optimal for consistent behavior with Brotli
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }

    /// <summary>
    /// Applies response compression middleware to the request pipeline.
    /// MUST be called early in the pipeline (before UseStaticFiles, UseRouting, etc.)
    /// to compress all downstream responses.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseTruLoadResponseCompression(this IApplicationBuilder app)
    {
        app.UseResponseCompression();
        return app;
    }
}
