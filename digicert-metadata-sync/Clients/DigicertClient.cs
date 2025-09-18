// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigicertMetadataSync.Client;
using DigicertMetadataSync.Models;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace DigicertMetadataSync.Client
{
    /// <summary>
    ///     Fully synchronous DigiCert CertCentral Services API client built on HttpClient.Send (requires .NET 8+).
    ///     Matches the style of KeyfactorMetadataClient (sync helpers, streaming JSON).
    ///     Docs:
    ///     - Auth header X-DC-DEVKEY
    ///     - Account custom fields: /services/v2/account/metadata
    ///     - Orders: /services/v2/order/certificate (list, info) and /custom-field (value edits)
    /// </summary>
    public sealed class DigiCertClient
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;

        private readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };

        public DigiCertClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        /// <summary>
        ///     Sets required headers. Provide your CertCentral API key.
        /// </summary>
        public void Authenticate(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey));
            var h = _http.DefaultRequestHeaders;
            if (h.Contains("X-DC-DEVKEY")) h.Remove("X-DC-DEVKEY");
            h.Add("X-DC-DEVKEY", apiKey);
            if (!h.Accept.Any()) h.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Many DigiCert examples include Content-Type on GET; unnecessary here.
        }

        // ------------------------- Core helpers -----------------------------
        private Uri BuildUri(string relativeOrAbsolute)
        {
            if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs)) return abs;
            var baseUri = _http.BaseAddress ??
                          throw new InvalidOperationException("HttpClient.BaseAddress must be set.");
            return new Uri(baseUri, relativeOrAbsolute.TrimStart('/'));
        }

        private HttpResponseMessage Send(HttpMethod method, string path, HttpContent? content = null)
        {
            using var req = new HttpRequestMessage(method, BuildUri(path));
            if (content != null) req.Content = content;
            var res = SendWithRetry(req);
            res.EnsureSuccessStatusCode();
            return res;
        }

        private T? ReadJson<T>(HttpResponseMessage resp)
        {
            using var s = resp.Content.ReadAsStream();
            return JsonSerializer.Deserialize<T>(s, _json);
        }

        private StringContent JsonBody<T>(T body)
        {
            return new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        }

        // --------------------- Account: Custom Fields -----------------------

        /// <summary>
        ///     GET /services/v2/account/metadata – lists custom order fields.
        /// </summary>
        public List<DcCustomField> ListCustomFields()
        {
            var resp = Send(HttpMethod.Get, "account/metadata");
            var root = ReadJson<DcCustomFieldListRoot>(resp) ?? new DcCustomFieldListRoot();
            return root.Metadata ?? new List<DcCustomField>();
        }

        /// <summary>
        ///     POST /services/v2/account/metadata – add a single custom field.
        /// </summary>
        public HttpResponseMessage AddCustomField(DcCustomFieldCreate create)
        {
            if (create is null) throw new ArgumentNullException(nameof(create));
            var resp = Send(HttpMethod.Post, "account/metadata", JsonBody(create));
            return resp;
        }

        /// <summary>
        ///     POST /services/v2/account/metadata/bulk – add multiple custom fields.
        /// </summary>
        public HttpResponseMessage BulkAddCustomFields(IEnumerable<DcCustomFieldCreate> items)
        {
            var body = new { metadata = items?.ToList() ?? new List<DcCustomFieldCreate>() };
            var resp = Send(HttpMethod.Post, "account/metadata/bulk", JsonBody(body));
            return resp;
        }

        /// <summary>
        ///     PUT /services/v2/account/metadata/{metadata_id}
        /// </summary>
        public DcCustomField EditCustomField(int metadataId, DcCustomFieldUpdate update)
        {
            if (metadataId <= 0) throw new ArgumentOutOfRangeException(nameof(metadataId));
            var resp = Send(HttpMethod.Put, $"account/metadata/{metadataId}", JsonBody(update));
            return ReadJson<DcCustomField>(resp) ?? new DcCustomField();
        }

        /// <summary>
        ///     DELETE /services/v2/account/metadata/{metadata_id}
        /// </summary>
        public void DeleteCustomField(int metadataId)
        {
            if (metadataId <= 0) throw new ArgumentOutOfRangeException(nameof(metadataId));
            using var _ = Send(HttpMethod.Delete, $"account/metadata/{metadataId}");
        }

        // --------------------------- Orders --------------------------------

        /// <summary>
        ///     Updates a single custom field value on a DigiCert order.
        ///     Throws HttpRequestException on non-success (incl. invalid_custom_field_value).
        /// </summary>
        public bool UpdateOrderCustomFieldValue(int orderId, int metadataId, string value)
        {
            if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
            if (metadataId <= 0) throw new ArgumentOutOfRangeException(nameof(metadataId));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Value must be non-empty. To remove a value, call DeleteOrderCustomFieldValue.", nameof(value));

            var url = $"order/certificate/{orderId}/custom-field";
            var payload = new DcCustomFieldValueUpdate { MetadataId = metadataId, Value = value };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8,
                    "application/json")
            };

            using var res = SendWithRetry(req); // your existing retry (429/5xx) helper
            if (res.IsSuccessStatusCode) return true;
            if (res.StatusCode != HttpStatusCode.NoContent)
            {
                var body = ReadString(res);
                _logger.Error("DigiCert custom-field update did not return 204. Status={Status}, Body={Body}",
                    res.StatusCode, body);
                res.EnsureSuccessStatusCode();
            }

            return false;
        }

        /// <summary>
        ///     GET /services/v2/order/certificate – list orders with optional filters.
        ///     Supply filters as a dictionary of key to value, e.g. { "serial_number", "..." }.
        /// </summary>
        public DcOrderList ListOrders(IDictionary<string, string>? filters = null, int? offset = null,
            int? limit = null)
        {
            var sb = new StringBuilder("order/certificate");
            var first = true;

            void AddQ(string k, string v)
            {
                sb.Append(first ? '?' : '&');
                first = false;
                sb.Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
            }

            if (filters != null)
                foreach (var kv in filters)
                    AddQ($"filters[{kv.Key}]", kv.Value);
            if (offset.HasValue) AddQ("offset", offset.Value.ToString());
            if (limit.HasValue) AddQ("limit", limit.Value.ToString());

            var resp = Send(HttpMethod.Get, sb.ToString());
            return ReadJson<DcOrderList>(resp) ?? new DcOrderList();
        }

        /// <summary>
        ///     Returns the full order (DcOrderInfo) including DcOrderCertificate and custom_fields.
        ///     Will use at most two requests (one if identifier substitution succeeds).
        /// </summary>
        public DcOrderInfo? GetOrderBySerialOrThumbprint(string? serialHex, string? thumbprint)
        {
            var normSerial = NormalizeHex(serialHex);
            var normThumb = NormalizeHex(thumbprint);

            // 1) Fast path: identifier substitution on /order/certificate/{identifier}
            if (!string.IsNullOrEmpty(normSerial))
            {
                var info = TryGetOrderInfoByIdentifier(normSerial);
                if (info != null) return info;

                // Fallback by serial -> order_id -> order
                var orderId = FindOrderIdBySerial(normSerial);
                if (orderId != null)
                    return TryGetOrderInfoByIdentifier(orderId.Value.ToString());
            }

            if (!string.IsNullOrEmpty(normThumb))
            {
                var info = TryGetOrderInfoByIdentifier(normThumb);
                if (info != null) return info;

                // Fallback by thumbprint via Custom Reports -> order_id -> order
                var orderId = FindOrderIdByThumbprintViaReports(normThumb);
                if (orderId != null)
                    return TryGetOrderInfoByIdentifier(orderId.Value.ToString());
            }

            _logger?.Warn("Unable to locate DigiCert order for serial {Serial} or thumbprint {Thumb}.",
                normSerial ?? "<null>", normThumb ?? "<null>");
            return null;
        }

        // ---------------------- Core calls ----------------------

        private DcOrderInfo? TryGetOrderInfoByIdentifier(string idOrSerialOrThumbprint)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"order/certificate/{Uri.EscapeDataString(idOrSerialOrThumbprint)}");
            using var res = SendWithRetry(req);
            if (res.StatusCode == HttpStatusCode.NotFound) return null;
            res.EnsureSuccessStatusCode();

            var info = ReadJson<DcOrderInfo>(res);
            if (info?.Certificate == null && info != null)
                _logger?.Debug("Order {OrderId} returned without certificate object.", info.Id);
            return info;
        }

        private int? FindOrderIdBySerial(string serialHex)
        {
            var url = $"order/certificate?filters[serial_number]={Uri.EscapeDataString(serialHex)}&limit=1";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = SendWithRetry(req);
            if (res.StatusCode == HttpStatusCode.NotFound) return null;
            res.EnsureSuccessStatusCode();

            var list = ReadJson<DcOrderList>(res);
            var id = list?.Orders?.FirstOrDefault()?.Id;
            _logger?.Debug("FindOrderIdBySerial({Serial}) -> {OrderId}", serialHex, id);
            return id;
        }

        private int? FindOrderIdByThumbprintViaReports(string thumbprint)
        {
            var payload = new ReportsQueryRequest
            {
                Query = "query($t:String!){ order_details(thumbprint:$t, limit:1){ id } }",
                Variables = new ReportsQueryVars { T = thumbprint }
            };

            using var req =
                new HttpRequestMessage(HttpMethod.Post, new Uri("https://www.digicert.com/services/v2/reports/query"))
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8,
                        "application/json")
                };

            using var res = SendWithRetry(req);
            if (res.StatusCode == HttpStatusCode.NotFound) return null;
            if (res.StatusCode == HttpStatusCode.BadRequest) return null;

            var doc = ReadJson<ReportsQueryResponse<ReportsOrderDetailsData>>(res);
            var first = doc?.Data?.OrderDetails?.FirstOrDefault()?.Id;
            if (first != null && int.TryParse(first, out var id))
            {
                _logger?.Debug("FindOrderIdByThumbprintViaReports({Thumb}) -> {OrderId}", thumbprint, id);
                return id;
            }

            _logger?.Debug("FindOrderIdByThumbprintViaReports({Thumb}) returned no matches.", thumbprint);
            return null;
        }

        private static string ReadString(HttpResponseMessage resp)
        {
            // Choose encoding from Content-Type if present; else detect BOM; else UTF-8
            var charset = resp.Content.Headers.ContentType?.CharSet;
            Encoding enc;
            try
            {
                enc = string.IsNullOrWhiteSpace(charset) ? new UTF8Encoding(false) : Encoding.GetEncoding(charset);
            }
            catch
            {
                enc = new UTF8Encoding(false);
            }

            using var s = resp.Content.ReadAsStream(); // sync
            using var sr = new StreamReader(s, enc, true, 8192, false);
            return sr.ReadToEnd();
        }

        private static byte[] ReadAllBytes(HttpContent content)
        {
            using var s = content.ReadAsStream(); // sync
            using var ms = new MemoryStream();
            s.CopyTo(ms); // sync
            return ms.ToArray();
        }

        private HttpResponseMessage SendWithRetry(HttpRequestMessage req)
        {
            const int maxAttempts = 4;
            var rnd = new Random();

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                HttpResponseMessage res;
                try
                {
                    res = _http.Send(req.Clone());
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    var delay1 = BackoffDelay(attempt, rnd);
                    _logger?.Warn(ex, "Request send failed (attempt {Attempt}/{Max}). Backing off {Delay}ms.", attempt,
                        maxAttempts, (int)delay1.TotalMilliseconds);
                    Thread.Sleep(delay1);
                    continue;
                }

                if (IsFinal(res.StatusCode))
                    return res;

                // 429/5xx -> back off, maybe honor Retry-After
                if (attempt == maxAttempts) return res;

                var delay2 = GetRetryAfter(res) ?? BackoffDelay(attempt, rnd);
                _logger?.Warn("Received {Status} (attempt {Attempt}/{Max}). Backing off {Delay}ms.",
                    (int)res.StatusCode, attempt, maxAttempts, (int)delay2.TotalMilliseconds);
                Thread.Sleep(delay2);
            }

            throw new InvalidOperationException("Unreachable retry loop termination.");
        }

        private static bool IsFinal(HttpStatusCode code)
        {
            if (code == (HttpStatusCode)429) return false; // Too Many Requests
            var n = (int)code;
            if (n >= 500 && n <= 599) return false; // 5xx transient
            return true; // all others are final
        }

        private static TimeSpan? GetRetryAfter(HttpResponseMessage res)
        {
            if (res.Headers.TryGetValues("Retry-After", out var vals))
            {
                var s = vals.FirstOrDefault();
                if (int.TryParse(s, out var seconds))
                    return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30));
            }

            return null;
        }

        private static TimeSpan BackoffDelay(int attempt, Random rnd)
        {
            // 250, 500, 1000 ms … with +/- up to 200 ms jitter
            var baseMs = (int)Math.Pow(2, attempt) * 250;
            return TimeSpan.FromMilliseconds(baseMs + rnd.Next(0, 200));
        }

        private static string? NormalizeHex(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            // keep only hex, make uppercase (handles serials and thumbprints)
            var filtered = new string(input.Where(Uri.IsHexDigit).ToArray());
            return filtered.Length == 0 ? null : filtered.ToUpperInvariant();
        }
    }
}

// HttpRequestMessage is single-use; clone content/headers for retries
// HttpRequestMessage is single-use; clone so we can retry safely
internal static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage Clone(this HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);

        // headers
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        // content (buffer if present)
        if (req.Content != null)
        {
            var bytes = req.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            var content = new ByteArrayContent(bytes);
            foreach (var h in req.Content.Headers)
                content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            clone.Content = content;
        }

        return clone;
    }
}

#region Models

public sealed class RateLimitException : Exception
{
    public RateLimitException(string message, int? retryAfterSeconds = null, Exception? inner = null)
        : base(message, inner)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int? RetryAfterSeconds { get; }
}

#endregion


public static class DigiCertServiceCollectionExtensions
{
    /// <summary>
    ///     Registers a DigiCertClient configured for CertCentral Services API.
    ///     BaseAddress should be set to "https://www.digicert.com/services/v2/".
    /// </summary>
    public static IServiceCollection AddDigiCertClient(this IServiceCollection services, string baseAddress)
    {
        services.AddHttpClient<DigiCertClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // DigiCert requires X-DC-DEVKEY; call Authenticate(apiKey) after DI construction.
        });
        return services;
    }
}