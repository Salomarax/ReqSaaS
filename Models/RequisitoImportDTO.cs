using System;

namespace ReqSaaS_1.Models
{
    /// <summary>
    /// DTO que representa lo mínimo que traeremos desde la BCN
    /// para luego mapear a la tabla Requisitos.
    /// (No es entidad de BD.)
    /// </summary>
    public class RequisitoImportDTO
    {
        public int NormaID_BCN { get; set; }            // idNorma (BCN) -> Requisitos.normaID_BCN
        public string? Titulo { get; set; }             // -> Requisitos.Titulo
        public string? Entidad { get; set; }            // -> Requisitos.Entidad (organismo emisor)
        public string? Tipo { get; set; }               // -> mapea a tu catálogo para Requisitos.ID_tipo
        public DateTime? FechaPromulgacion { get; set; }// (opcional, referencia)
        public string? Descripcion { get; set; }        // breve resumen si lo obtenemos
        public string FuenteUrl { get; set; } = "";     // URL exacta consultada (referencia)
        public string XmlRaw { get; set; } = "";        // XML crudo (debug/backup)
    }
}
