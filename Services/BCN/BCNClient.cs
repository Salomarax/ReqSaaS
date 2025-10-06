using ReqSaaS_1.Models;
using System.Net.Http.Headers;

namespace ReqSaaS_1.Services.BCN
{
    public class BCNClient : IBCNClient
    {
        private readonly HttpClient _http;

        public BCNClient(HttpClient http)
        {
            _http = http;
            _http.BaseAddress ??= new Uri("https://www.leychile.cl/");
            _http.Timeout = TimeSpan.FromSeconds(15);
            if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
                _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReqSaaS", "1.0"));
        }

        public async Task<string?> GetNormaXmlAsync(int idNorma, CancellationToken ct = default)
        {
            var url = $"Consulta/obtxml?opt=7&idNorma={idNorma}";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }

        // Stub para cumplir la interfaz // por ahora no esta habilitada la búsqueda por texto
        public Task<IReadOnlyList<BCNSearchResultDto>> SearchNormasAsync(string query, CancellationToken ct)
        {
            IReadOnlyList<BCNSearchResultDto> empty = Array.Empty<BCNSearchResultDto>();
            return Task.FromResult(empty);
        }
    }
}
