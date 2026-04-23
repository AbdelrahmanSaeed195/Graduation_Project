using System.ComponentModel.DataAnnotations;

namespace projectweb.ViewModel
{
    public class RoleViewModel
    {
        [Required]
        public string RoleType { get; set; }
    }
}
