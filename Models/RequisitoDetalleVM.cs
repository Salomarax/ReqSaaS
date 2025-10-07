using System.Collections.Generic;
using ReqSaaS_1.Data.Entities;

namespace ReqSaaS_1.Models
{
    public class RequisitoDetalleVM
    {
        public int IdReq { get; set; }
        public string Titulo { get; set; } = "";
        public string? Entidad { get; set; }
        public int? IdTipo { get; set; }
        public string? TipoNombre { get; set; }
        public int? NormaIDBCN { get; set; }

        // Usamos la entidad directamente:
        public List<DetalleEvaluacion> Items { get; set; } = new();
    }
}
