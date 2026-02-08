using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Infrastructure;

public sealed class RetryPolicy
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxAttempts,
        TimeSpan baseDelay,
        double backoffFactor,
        CancellationToken token)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        var attempt = 0;
        Exception? lastError = null;
        while (attempt < maxAttempts)
        {
            token.ThrowIfCancellationRequested();
            attempt++;
            try
            {
                return await action(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (attempt >= maxAttempts)
            {
                break;
            }

            var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(backoffFactor, attempt - 1));
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, token);
            }
        }

        throw lastError ?? new InvalidOperationException("Retry policy failed without exception.");
    }

    public async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        int maxAttempts,
        TimeSpan baseDelay,
        double backoffFactor,
        CancellationToken token)
    {
        return await ExecuteAsync(async ct =>
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            try
            {
                return await action(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException("処理がタイムアウトしました");
            }
        }, maxAttempts, baseDelay, backoffFactor, token);
    }
}
