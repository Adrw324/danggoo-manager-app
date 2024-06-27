using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DanggooManager.Models
{
    public class Account
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public double Average { get; set; }
    public int TotalPlay { get; set; }
    public int TotalScore { get; set; }
}
}