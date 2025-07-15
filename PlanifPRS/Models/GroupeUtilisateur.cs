namespace PlanifPRS.Models
{
    public class GroupeUtilisateur
    {
        public int GroupeId { get; set; }
        public int UtilisateurId { get; set; }
        public DateTime DateAjout { get; set; } = DateTime.Now;

        public GroupeUtilisateurs Groupe { get; set; }
        public Utilisateur Utilisateur { get; set; }
    }
}