using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PlanifPRS.Models
{
    [Table("PRS_LiensDossierPRS")]
    public class LienDossierPrs
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PrsId { get; set; }

        [Required]
        [StringLength(500)]
        public string Chemin { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        public DateTime DateAjout { get; set; } = DateTime.Now;

        [Required]
        [StringLength(100)]
        public string AjouteParLogin { get; set; }

        [ForeignKey("PrsId")]
        [ValidateNever]
        public virtual Prs Prs { get; set; }
    }
}