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
        public int PrsId { get; set; }

        [Required, MaxLength(255)]
        public string NomOriginal { get; set; }

        [Required, MaxLength(500)]
        public string CheminFichier { get; set; }

        [MaxLength(100)]
        public string TypeMime { get; set; }

        public long Taille { get; set; }

        public DateTime DateUpload { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string UploadParLogin { get; set; }

        // Propriété de navigation
        [ValidateNever]
        public virtual Prs Prs { get; set; }
    }
}