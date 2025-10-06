using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReqSaaS_1.Data.Entities
{
    [Table("Tipo", Schema = "public")]
    public class Tipo
    {
        [Key]
        [Column("ID_tipo")]
        public int IdTipo { get; set; }

        [Column("Nombre")]
        public string Nombre { get; set; } = "";
    }
}
