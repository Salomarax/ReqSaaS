using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReqSaaS_1.Data.Entities
{
    [Table("Requisitos", Schema = "public")]
    public class Requisito
    {
        [Key]
        [Column("ID_requisito")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdReq { get; set; }

        [Column("ID_organismo")]
        public string? IdOrganismo { get; set; }

        [Column("Titulo")]
        public string? Titulo { get; set; }

        [Column("Descripcion")]
        public string? Descripcion { get; set; }

        [Column("Entidad")]
        public string? Entidad { get; set; }

        
        [Column("PorcentajeCumplimiento")]
        public int? PorcentajeCumplimiento { get; set; }

        [Column("ID_tipo")]
        public int? IdTipo { get; set; }

        
        [Column("normaID_BCN")]
        public int? NormaIDBCN { get; set; }

        public ICollection<DetalleEvaluacion> DetalleEvaluaciones { get; set; } = new List<DetalleEvaluacion>();
    }
}
