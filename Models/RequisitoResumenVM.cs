// Models/RequisitoResumenVM.cs
namespace ReqSaaS_1.Models
{
    public class RequisitoResumenVM
    {
        public int IdRequisito { get; set; }
        public string Titulo { get; set; } = "";
        public string? Entidad { get; set; }
        public string? Tipo { get; set; }
        public int TotalArticulos { get; set; }
        public int Cumplidos { get; set; }
        public int Porcentaje { get; set; }
        public string NumeroEtiqueta => $"REQ-{IdRequisito}";
    }
}
