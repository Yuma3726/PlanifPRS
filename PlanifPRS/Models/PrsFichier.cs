using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlanifPRS.Models
{
    public class PrsFichier
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Prs")]
        public int PRSId { get; set; }

        [ValidateNever]
        public Prs Prs { get; set; }

        [Required]
        [MaxLength(255)]
        public string NomFichier { get; set; }

        [Required]
        [MaxLength(500)]
        public string CheminFichier { get; set; }

        public long TailleFichier { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.Now;
    }
}