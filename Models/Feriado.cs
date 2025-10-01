namespace ReqSaaS_1.Models
{
    public class Feriado
    {
        private string fecha;
        private string nombre;
        private bool irrenunciable;

        // get y set Fecha 
        public string Fecha
        {
            get
            {
                return fecha;
            }
            set
            {
                fecha = value;
            }
        }

        // get y set Nombre
        public string Nombre
        {
            get
            {
                return nombre;
            }
            set
            {
                nombre = value;
            }
        }

        // get y set 
        public bool Irrenunciable
        {
            get
            {
                return irrenunciable;
            }
            set
            {
                irrenunciable = value;
            }
        }
    }

}
