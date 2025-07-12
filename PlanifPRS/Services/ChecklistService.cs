using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;

namespace PlanifPRS.Services
{
    public class ChecklistService
    {
        private readonly PlanifPrsDbContext _context;

        public ChecklistService(PlanifPrsDbContext context)
        {
            _context = context;
        }

        // Méthodes pour les modèles de checklist
        public async Task<List<ChecklistModele>> GetChecklistModelesAsync()
        {
            return await _context.ChecklistModeles
                .Include(cm => cm.Elements)
                .Where(cm => cm.Actif)
                .OrderBy(cm => cm.Nom)
                .ToListAsync();
        }

        public async Task<ChecklistModele?> GetChecklistModeleByIdAsync(int id)
        {
            return await _context.ChecklistModeles
                .Include(cm => cm.Elements)
                .FirstOrDefaultAsync(cm => cm.Id == id && cm.Actif);
        }

        public async Task<bool> CreateChecklistModeleAsync(ChecklistModele modele)
        {
            try
            {
                _context.ChecklistModeles.Add(modele);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateChecklistModeleAsync(ChecklistModele modele)
        {
            try
            {
                _context.ChecklistModeles.Update(modele);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteChecklistModeleAsync(int id)
        {
            var modele = await _context.ChecklistModeles.FindAsync(id);
            if (modele == null) return false;

            modele.Actif = false;
            await _context.SaveChangesAsync();
            return true;
        }

        // Méthodes pour les checklists PRS
        public async Task<List<PrsChecklist>> GetPrsChecklistAsync(int prsId)
        {
            return await _context.PrsChecklists
                .Include(pc => pc.Famille)
                .Include(pc => pc.ChecklistModeleSource)
                .Where(pc => pc.PRSId == prsId)
                .OrderBy(pc => pc.Priorite)
                .ThenBy(pc => pc.DelaiDefautJours)
                .ThenBy(pc => pc.DateEcheance)
                .ThenBy(pc => pc.Categorie)
                .ThenBy(pc => pc.SousCategorie)
                .ToListAsync();
        }

        public async Task<bool> ApplyChecklistModeleAsync(int prsId, int checklistModeleId, string userLogin)
        {
            try
            {
                var modele = await GetChecklistModeleByIdAsync(checklistModeleId);
                if (modele == null) return false;

                // Supprimer les éléments existants de la checklist PRS
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == prsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Récupérer les infos du PRS pour calculer les échéances
                var prs = await _context.Prs.FindAsync(prsId);
                if (prs == null) return false;

                // Créer les nouveaux éléments basés sur le modèle
                foreach (var element in modele.Elements.OrderBy(e => e.Priorite))
                {
                    var prsChecklistItem = new PrsChecklist
                    {
                        PRSId = prsId,
                        Categorie = element.Categorie,
                        SousCategorie = element.SousCategorie,
                        Libelle = element.Libelle,
                        Tache = element.Libelle, // Compatibilité avec l'ancien système
                        Priorite = element.Priorite,
                        DelaiDefautJours = element.DelaiDefautJours,
                        Obligatoire = element.Obligatoire,
                        EstCoche = false,
                        Statut = null,
                        DateEcheance = CalculerDateEcheance(prs, element),
                        ChecklistModeleSourceId = checklistModeleId,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now
                    };

                    _context.PrsChecklists.Add(prsChecklistItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'application du modèle de checklist: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CopyChecklistFromPrsAsync(int targetPrsId, int sourcePrsId, string userLogin)
        {
            try
            {
                var sourceChecklist = await GetPrsChecklistAsync(sourcePrsId);
                if (!sourceChecklist.Any()) return false;

                // Supprimer les éléments existants
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == targetPrsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Récupérer les infos du PRS cible
                var targetPrs = await _context.Prs.FindAsync(targetPrsId);
                if (targetPrs == null) return false;

                // Copier les éléments
                foreach (var sourceItem in sourceChecklist)
                {
                    var newItem = new PrsChecklist
                    {
                        PRSId = targetPrsId,
                        Categorie = sourceItem.Categorie,
                        SousCategorie = sourceItem.SousCategorie,
                        Libelle = sourceItem.Libelle,
                        Tache = sourceItem.Tache,
                        Priorite = sourceItem.Priorite,
                        DelaiDefautJours = sourceItem.DelaiDefautJours,
                        Obligatoire = sourceItem.Obligatoire,
                        EstCoche = false,
                        Statut = null,
                        DateEcheance = CalculerDateEcheanceFromDelai(targetPrs, sourceItem.DelaiDefautJours),
                        PrsSourceId = sourcePrsId,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now
                    };

                    _context.PrsChecklists.Add(newItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CreateCustomChecklistAsync(int prsId, List<PrsChecklist> elements, string userLogin)
        {
            try
            {
                // Supprimer les éléments existants
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == prsId)
                    .ToListAsync();

                _context.PrsChecklists.RemoveRange(existingItems);

                // Récupérer les infos du PRS
                var prs = await _context.Prs.FindAsync(prsId);
                if (prs == null) return false;

                // Ajouter les nouveaux éléments
                foreach (var element in elements)
                {
                    element.PRSId = prsId;
                    element.EstCoche = false;
                    element.Statut = null;
                    element.DateEcheance = CalculerDateEcheanceFromDelai(prs, element.DelaiDefautJours);
                    element.CreatedByLogin = userLogin;
                    element.DateCreation = DateTime.Now;

                    _context.PrsChecklists.Add(element);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private DateTime? CalculerDateEcheance(Prs prs, ChecklistElementModele element)
        {
            if (element.DelaiDefautJours <= 0) return null;
            return prs.DateCreation.AddDays(element.DelaiDefautJours);
        }

        private DateTime? CalculerDateEcheanceFromDelai(Prs prs, int delaiJours)
        {
            if (delaiJours <= 0) return null;
            return prs.DateCreation.AddDays(delaiJours);
        }
    }
}