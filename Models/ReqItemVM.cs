using System.ComponentModel.DataAnnotations;

namespace ReqSaaS_1.Models
{
    public class ReqItemVm
    {
        [Required]
        public string Nombre { get; set; } = "";   // p.ej. "Artículo 7"
        public string Detalle { get; set; } = "";  // texto del requerimiento
    }
}
