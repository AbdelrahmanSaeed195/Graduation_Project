using System.ComponentModel.DataAnnotations;

namespace projectweb.ViewModel
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}