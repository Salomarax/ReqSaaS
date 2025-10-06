using ReqSaaS_1.Models;

namespace ReqSaaS_1.Services.BCN
{
    public interface IBCNClient
    {
        //  búsqueda por palabra clave (o frase)
        Task<string?> GetNormaXmlAsync(int idNorma, CancellationToken ct = default);

        //  búsqueda por palabra clave (o frase)
        Task<IReadOnlyList<BCNSearchResultDto>> SearchNormasAsync(string query, CancellationToken ct);

    }
}
