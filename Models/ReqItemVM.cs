using System.ComponentModel.DataAnnotations;

namespace ReqSaaS_1.Models
{
    public class RequirementItemVm
    {
        [Required]
        public string Nombre { get; set; } = "";   // p.ej. "Artículo 7"
        public string Detalle { get; set; } = "";  // texto del requerimiento
    }

    public class RequirementCreateVm
    {
        [Required, Display(Name = "Título del Requisito")]
        public string Titulo { get; set; } = "";

        [Required, Display(Name = "Descripción")]
        public string Descripcion { get; set; } = "";

        [Required, Display(Name = "Entidad que emite")]
        public string EntidadEmisora { get; set; } = "";

        [Required, Display(Name = "Tipo de requisito")]
        public string Tipo { get; set; } = "";     // Ley, Norma, Regulación, Decreto

        // Items / artículos
        public List<RequirementItemVm> Items { get; set; } = new() {
            new RequirementItemVm { Nombre = "Artículo 1", Detalle = "" }
        };

        // Aux para “Importar BCN”
        public string? BcnId { get; set; }
    }
}
