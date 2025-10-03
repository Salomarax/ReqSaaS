using System.ComponentModel.DataAnnotations;

namespace ReqSaaS_1.Models
{
    public class LoginVM
    {
        [Required]
        public string Rut { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
