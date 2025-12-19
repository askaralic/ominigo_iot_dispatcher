using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TelemetryDBBridgeService.Json;
using TelemetryDBBridgeService.Models;
using TelemetryDBBridgeService.Options;

namespace TelemetryDBBridgeService.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ApiOptions _options;
    private readonly ILogger<ApiClient> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient httpClient, IOptions<ApiOptions> options, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _serializerOptions.Converters.Add(new UnixSecondsDateTimeOffsetConverter());
        _serializerOptions.Converters.Add(new NullableUnixSecondsDateTimeOffsetConverter());

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.AuthToken);
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<QueueItemDto>> GetQueueAsync(int limit, string lockedBy, CancellationToken cancellationToken)
    {
        var request = new GetQueueRequest
        {
            Limit = limit,
            LockedBy = lockedBy
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, _options.GetQueueEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, _serializerOptions), Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetQueueAsync responded with status code {StatusCode}", response.StatusCode);
                return new List<QueueItemDto>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("GetQueueAsync returned empty body.");
                return new List<QueueItemDto>();
            }

            // Attempt legacy wrapped response: { status, message, data: [...] }
            try
            {
                var payload = JsonSerializer.Deserialize<GetQueueResponse>(content, _serializerOptions);
                if (payload?.Status == true && payload.Data != null)
                {
                    return payload.Data;
                }

                _logger.LogWarning("GetQueueAsync returned unsuccessful payload. Status: {Status}, Message: {Message}", payload?.Status, payload?.Message);
                return new List<QueueItemDto>();
            }
            catch (JsonException)
            {
                // Ignore and try alternative shapes.
            }

            // Attempt direct array: [ { queue item }, ... ]
            try
            {
                var arrayResult = JsonSerializer.Deserialize<List<QueueItemDto>>(content, _serializerOptions);
                if (arrayResult != null)
                {
                    return arrayResult;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize queue response as array. Falling back to single item.");
            }

            // Attempt single item object.
            try
            {
                var singleResult = JsonSerializer.Deserialize<QueueItemDto>(content, _serializerOptions);
                if (singleResult != null)
                {
                    return new List<QueueItemDto> { singleResult };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize queue response as single queue item.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetQueueAsync");
        }

        return new List<QueueItemDto>();
    }

    public async Task<bool> UpdateQueueAsync(UpdateQueueRequest updateRequest, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, _options.UpdateQueueEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(updateRequest, _serializerOptions), Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UpdateQueueAsync responded with status code {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("UpdateQueueAsync returned empty body for dispatch_queue_uno {DispatchQueueUno}", updateRequest.DispatchQueueUno);
                return false;
            }

            var payload = JsonSerializer.Deserialize<UpdateQueueResponse>(content, _serializerOptions);
            if (payload?.Status == true)
            {
                return true;
            }

            _logger.LogWarning("UpdateQueueAsync returned unsuccessful payload. Status: {Status}, Message: {Message}", payload?.Status, payload?.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling UpdateQueueAsync for dispatch_queue_uno {DispatchQueueUno}", updateRequest.DispatchQueueUno);
        }

        return false;
    }
}
