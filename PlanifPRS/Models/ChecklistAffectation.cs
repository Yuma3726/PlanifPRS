using PlanifPRS.Models;

public class ChecklistAffectation
{
    public int Id { get; set; }
    public int ChecklistId { get; set; }
    public int? UtilisateurId { get; set; }
    public int? GroupeId { get; set; }
    public string TypeAffectation { get; set; }
    public DateTime DateAffectation { get; set; }
    public string AffectePar { get; set; }

    // Navigation properties
    public virtual PrsChecklist Checklist { get; set; }
    public virtual Utilisateur Utilisateur { get; set; }
    public virtual GroupeUtilisateurs Groupe { get; set; }
}