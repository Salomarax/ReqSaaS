using System.ComponentModel.DataAnnotations;

namespace ReqSaaS_1.Models
{
    public class ReqInputVM
    {
        [Required] public string Titulo { get; set; } = "";
        [Required] public string Descripcion { get; set; } = "";
        [Required] public string Entidad { get; set; } = "";
        [Required] public string Tipo { get; set; } = "";   // Ley/Norma/Regulación/Decreto
        [Required] public string Item { get; set; } = "";   // Artículo o requerimiento
    }
}
