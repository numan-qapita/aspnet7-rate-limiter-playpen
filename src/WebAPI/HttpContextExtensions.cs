using System.Net;

namespace WebAPI;

public static class HttpContextExtensions
{
    public static (string ipAddress, string source) GetClientIp(this HttpContext httpContext)
    {
        const string httpContextSource = "HttpContext RemoteIpAddress";
        const string realIpHeader = "X-Real-IP";
        const string forwardedForHeader = "X-Forwarded-For";

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        var result = remoteIp?.MapToIPv4().ToString() ?? string.Empty;

        // If the IP is not loopback, then we can already return.
        if (remoteIp != null && !IPAddress.IsLoopback(remoteIp))
        {
            return (result, httpContextSource);
        }

        var headers = httpContext.Request.Headers;

        // If the resolved IP is a loopback address, then we further check if it is from proxy.
        // If the request is from a proxy (generally NGINX uses this header), get the real client IP from the X-Real-IP header
        if (headers.TryGetValue(realIpHeader, out var realIp) &&
            !string.IsNullOrWhiteSpace(realIp))
        {
            return (realIp, realIpHeader);
        }

        // If the request is from a proxy, get the client IP from X-Forwarded-For header
        if (headers.TryGetValue(forwardedForHeader, out var forwardedForIp) &&
            !string.IsNullOrWhiteSpace(forwardedForIp))
        {
            return (forwardedForIp, forwardedForHeader);
        }

        return (result, httpContextSource);
    }
    
    public static string ResolveClientIpAddress(this HttpContext context)
    {
        // Common header for proxies
        string[] xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',');
        if (xForwardedFor?.Length > 0)
        {
            // Choose the IP farthest from the server as the originating IP.
            string externalIp = xForwardedFor.First().Trim();
            if (IPAddress.TryParse(externalIp, out IPAddress ip))
            {
                return ip.ToString();
            }
        }

        // Header commonly used by proxies
        string xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            if (IPAddress.TryParse(xRealIp, out IPAddress ip))
            {
                return ip.ToString();
            }
        }

        // Azure's header for client IP
        string xClientIp = context.Request.Headers["X-Client-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xClientIp))
        {
            if (IPAddress.TryParse(xClientIp, out IPAddress ip))
            {
                return ip.ToString();
            }
        }

        // When running behind a load balancer, you'll need to use the X-Forwarded-Proto, X-Forwarded-Host, and X-Forwarded-Port headers
        // to reconstruct the original request URI.
        
        // Fallback to remote IP if all else fails
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}