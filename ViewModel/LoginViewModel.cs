using System.ComponentModel.DataAnnotations;

namespace projectweb.ViewModel
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        public bool IsPrisist { get; set; }

    }
}
