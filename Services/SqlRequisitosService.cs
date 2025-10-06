using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReqSaaS_1.Data;

namespace ReqSaaS_1.Services;

public interface ISqlRequisitosService
{
    Task<int> UpsertRequisitoAsync(
        string titulo,
        string descripcion,
        string entidad,
        decimal? porcentajeCumplimiento,
        string idOrganismo,   // RUT (varchar)
        int normaIdBcn,
        int? idTipo,
        CancellationToken ct);

    Task<int?> InsertDetalleAsync(
        int idRequisito,
        string detalle,
        CancellationToken ct);
}

public class SqlRequisitosService : ISqlRequisitosService
{
    private readonly AppDbContext _db;

    public SqlRequisitosService(AppDbContext db) => _db = db;

    public async Task<int> UpsertRequisitoAsync(
        string titulo,
        string descripcion,
        string entidad,
        decimal? porcentajeCumplimiento,
        string idOrganismo,
        int normaIdBcn,
        int? idTipo,
        CancellationToken ct)
    {
        await using var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        const string sql = @"select public.insertrequisitos(
                                @titulo, @descripcion, @entidad,
                                @porc, @idorg, @norma, @tipo
                             )";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("titulo", titulo ?? string.Empty);
        cmd.Parameters.AddWithValue("descripcion", descripcion ?? string.Empty);
        cmd.Parameters.AddWithValue("entidad", entidad ?? string.Empty);
        cmd.Parameters.AddWithValue("porc", (object?)porcentajeCumplimiento ?? 0m);
        cmd.Parameters.AddWithValue("idorg", idOrganismo ?? string.Empty); // varchar
        cmd.Parameters.AddWithValue("norma", normaIdBcn);
        cmd.Parameters.AddWithValue("tipo", (object?)idTipo ?? DBNull.Value);

        var json = (string?)await cmd.ExecuteScalarAsync(ct) ?? "{}";
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("id_requisito", out var idProp) &&
            idProp.ValueKind == JsonValueKind.Number)
            return idProp.GetInt32();

        // Si tu función devuelve otro formato, puedes ajustar aquí.
        throw new InvalidOperationException("La función insertrequisitos no devolvió id_requisito.");
    }

    public async Task<int?> InsertDetalleAsync(
        int idRequisito,
        string detalle,
        CancellationToken ct)
    {
        await using var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        const string sql = @"select public.insertdetalleevaluacion(@id, @detalle)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", idRequisito);
        cmd.Parameters.AddWithValue("detalle", detalle ?? string.Empty);

        var json = (string?)await cmd.ExecuteScalarAsync(ct) ?? "{}";
        using var doc = JsonDocument.Parse(json);

        // La función retorna { status:'ok', id_item: <int|null> } si usas la versión que te propuse.
        if (doc.RootElement.TryGetProperty("id_item", out var idProp) &&
            idProp.ValueKind == JsonValueKind.Number)
            return idProp.GetInt32();

        // Si fue ON CONFLICT DO NOTHING, id_item podría ser null → devolvemos null
        return null;
    }
}
