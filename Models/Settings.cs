using System.ComponentModel.DataAnnotations;

namespace DanggooManager.Models
{
    public class Settings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Fee Per Minute")]
        [Range(0.01, 1000.00)]
        public decimal FeePerMinute { get; set; }
    }
}