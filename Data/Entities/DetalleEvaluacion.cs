using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ReqSaaS_1.Data.Entities
{
    [Table("DetalleEvaluacion", Schema = "public")]
    public class DetalleEvaluacion
    {
        // PK
        [Key]
        [Column("ID_item")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdItem { get; set; }

        // FK a Requisitos.ID_requisito
        [Column("ID_requisito")]
        public int IdRequisito { get; set; }

        // Campos de la tabla
        [Column("Justificacion")]
        public string? Justificacion { get; set; }

        [Column("Cumplimiento")]
        public bool Cumplimiento { get; set; }

        [Column("Archivo_url")]
        public string? ArchivoUrl { get; set; }

        [Column("Detalle")]
        public string? Detalle { get; set; }

        // Navegación
        [BindNever]
        public Requisito Requisito { get; set; } = null!;
    }
}
