using Microsoft.AspNetCore.Builder;

namespace Lintel.AspNetCore.Http;

/// <summary>
/// Requests that the client closed itself.
/// </summary>
public static class ClientDisconnectExtensions
{
    /// <summary>The status code of a request closed by the client (the code nginx uses): 499.</summary>
    public const int ClientClosedRequestStatusCode = 499;

    /// <summary>
    /// Ends a request quietly when it was cancelled because the client closed it (for example search as you type
    /// cancels the previous request): the <see cref="OperationCanceledException"/> becomes status 499 without an error
    /// page and without an error in the log. A cancellation for any other reason (a timeout of your own) stays an error.
    /// </summary>
    /// <remarks>
    /// Place it right after <c>UseExceptionHandler</c>, so that such requests do not reach the exception handler.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseClientDisconnectHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The client does not wait for the response any more.
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = ClientClosedRequestStatusCode;
                }
            }
        });
    }
}
