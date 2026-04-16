namespace ReqSaaS_1.Models
{
    public class CreateRequirementVM
    {
        // Campos del formulario
        public string Titulo { get; set; } = "";
        public string Entidad { get; set; } = "";
        public string Descripcion { get; set; } = "";

        // Selección de tipo (desde catálogo)
        public int? TipoId { get; set; }

        // Ítems/artículos dinámicos del formulario (Items[0], Items[1], ...)
        public List<string> Items { get; set; } = new();

        // Para renderizar el <select>
        public IEnumerable<ReqSaaS_1.Data.Entities.Tipo> Tipos { get; set; } = Enumerable.Empty<ReqSaaS_1.Data.Entities.Tipo>();
    }
}
