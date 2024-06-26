using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DanggooManager.Models
{
    public class Game
    {
        public int Id { get; set; }
        public int Table_Num { get; set; }

        [DisplayFormat(DataFormatString = "{0:MM/dd/yyyy}")]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [DisplayFormat(DataFormatString = "{0:HH:mm tt MM/dd/yyyy}")]
        [Display(Name = "Start")]
        [DataType(DataType.Time)]
        public DateTime Start { get; set; }

        [DisplayFormat(DataFormatString = "{0:HH:mm tt MM/dd/yyyy}")]
        [Display(Name = "End")]
        [DataType(DataType.Time)]
        public DateTime End { get; set; }

        public int Playtime { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Fee { get; set; }

        public bool finished { get; set; }
    }
}