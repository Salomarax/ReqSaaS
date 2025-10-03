using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReqSaaS_1.Data.Entities
{
    [Table("Credencial")]
    public class Credencial
    {
        [Key]
        [Column("ID_credencial")]
        public int IdCredencial { get; set; }

        [Column("ID_organismo")]
        public string IdOrganismo { get; set; } = null!;   // <- requerido en BD

        [Column("Clave_hash")]
        public string ClaveHash { get; set; } = null!;     // <- requerido en BD

        [Column("Nombre")]
        public string Nombre { get; set; } = string.Empty; // si en BD permite null, cámbialo a string?

        [Column("ID_nivel")]
        public int? IdNivel { get; set; }                  // si tu FK es NOT NULL, usa int
    }
}
