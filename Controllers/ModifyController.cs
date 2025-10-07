using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReqSaaS_1.Data;

[Authorize]
public class ModifyController : Controller
{
    private readonly AppDbContext _db;
    public ModifyController(AppDbContext db) => _db = db;   // <- ¡ojo! nombre correcto

    private bool CanCrud()
    {
        var nivel = User.FindFirst("nivel")?.Value ?? "1";
        return nivel == "2" || nivel == "3";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReq(int id, CancellationToken ct)
    {
        // Permisos
        var nivel = User.FindFirst("nivel")?.Value ?? "1";
        if (nivel != "2" && nivel != "3") return Forbid();

        var idOrganismo = User.FindFirst("rut")?.Value ?? "";

        var req = await _db.Requisitos
            .FirstOrDefaultAsync(r => r.IdReq == id && r.IdOrganismo == idOrganismo, ct);

        if (req == null)
        {
            TempData["Toast.Error"] = "Requisito no encontrado o no pertenece a su organismo.";
            return RedirectToAction(nameof(HomeController.reqView), "Home");
        }

        await using var trx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1) Borrar dependientes
            // Si usas EF Core 7/8, esto es lo más eficiente:
            await _db.DetalleEvaluaciones
                .Where(d => d.IdRequisito == id)
                .ExecuteDeleteAsync(ct);

            // Si tu versión no soporta ExecuteDeleteAsync, usa este fallback:
            // var hijos = await _db.DetalleEvaluaciones.Where(d => d.IdRequisito == id).ToListAsync(ct);
            // _db.DetalleEvaluaciones.RemoveRange(hijos);

            // 2) Borrar el padre
            _db.Requisitos.Remove(req);
            await _db.SaveChangesAsync(ct);

            await trx.CommitAsync(ct);
            TempData["Toast.Success"] = "Se eliminó el requisito y todas sus evidencias.";
        }
        catch (DbUpdateException)
        {
            await trx.RollbackAsync(ct);
            TempData["Toast.Error"] = "No se pudo eliminar por restricciones de integridad.";
        }
        catch
        {
            await trx.RollbackAsync(ct);
            TempData["Toast.Error"] = "Ocurrió un error inesperado al eliminar.";
        }

        return RedirectToAction(nameof(HomeController.reqView), "Home");
    }
}
