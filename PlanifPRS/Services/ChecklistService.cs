using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using Microsoft.Extensions.Logging;

namespace PlanifPRS.Services
{
    public class ChecklistService
    {
        private readonly PlanifPrsDbContext _context;
        private readonly ILogger<ChecklistService> _logger;

        public ChecklistService(PlanifPrsDbContext context, ILogger<ChecklistService> logger)
        {
            _context = context;
            _logger = logger;
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
                _logger.LogError($"Erreur lors de l'application du modèle de checklist: {ex.Message}");
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

                // Récupérer les infos du PRS cible et source
                var targetPrs = await _context.Prs.FindAsync(targetPrsId);
                var sourcePrs = await _context.Prs.FindAsync(sourcePrsId);
                if (targetPrs == null || sourcePrs == null) return false;

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
                        DateEcheance = RecalculerDateEcheance(sourcePrs, targetPrs, sourceItem.DateEcheance),
                        PrsSourceId = sourcePrsId,
                        CreatedByLogin = userLogin,
                        DateCreation = DateTime.Now
                    };

                    _context.PrsChecklists.Add(newItem);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de la copie de checklist: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateCustomChecklistAsync(int prsId, List<PrsChecklist> elements, string userLogin)
        {
            try
            {
                _logger.LogInformation($"[CreateCustomChecklist] Début - PRS ID: {prsId}, Éléments: {elements.Count}, User: {userLogin}");

                // Supprimer les éléments existants de la checklist PRS
                var existingItems = await _context.PrsChecklists
                    .Where(pc => pc.PRSId == prsId)
                    .ToListAsync();

                _logger.LogInformation($"[CreateCustomChecklist] Éléments existants trouvés: {existingItems.Count}");

                _context.PrsChecklists.RemoveRange(existingItems);

                // Récupérer la PRS pour le calcul des dates
                var prs = await _context.Prs.FindAsync(prsId);
                if (prs == null)
                {
                    _logger.LogError($"[CreateCustomChecklist] PRS {prsId} non trouvée");
                    return false;
                }

                // Ajouter les nouveaux éléments
                foreach (var element in elements)
                {
                    element.PRSId = prsId;
                    element.CreatedByLogin = userLogin;
                    element.DateCreation = DateTime.Now;

                    // CORRECTION : Calculer la DateEcheance si un délai est défini (X jours AVANT la PRS)
                    if (element.DelaiDefautJours > 0)
                    {
                        DateTime dateDebut = prs.DateDebut != default(DateTime) ? prs.DateDebut : prs.DateCreation;
                        element.DateEcheance = dateDebut.AddDays(-element.DelaiDefautJours); // SOUSTRACTION

                        _logger.LogInformation($"[CreateCustomChecklist] DateEcheance calculée pour {element.Libelle}: {element.DateEcheance} ({element.DelaiDefautJours} jours avant {dateDebut:yyyy-MM-dd})");
                    }
                    else
                    {
                        element.DateEcheance = null;
                        _logger.LogInformation($"[CreateCustomChecklist] Pas de délai défini pour {element.Libelle}");
                    }

                    _logger.LogInformation($"[CreateCustomChecklist] Ajout élément: {element.Libelle}, Catégorie: {element.Categorie}, DelaiDefautJours: {element.DelaiDefautJours}");
                    _context.PrsChecklists.Add(element);
                }

                var changes = await _context.SaveChangesAsync();
                _logger.LogInformation($"[CreateCustomChecklist] Sauvegarde réussie - {changes} modifications");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[CreateCustomChecklist] Erreur: {ex.Message}");
                _logger.LogError($"[CreateCustomChecklist] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private DateTime? CalculerDateEcheance(Prs prs, ChecklistElementModele element)
        {
            if (element.DelaiDefautJours <= 0) return null;

            DateTime dateDebut;
            if (prs.DateDebut != default(DateTime))
                dateDebut = prs.DateDebut;
            else
                dateDebut = prs.DateCreation;

            return dateDebut.AddDays(-element.DelaiDefautJours); // SOUSTRACTION - X jours AVANT la PRS
        }

        private DateTime? RecalculerDateEcheance(Prs sourcePrs, Prs targetPrs, DateTime? sourceEcheance)
        {
            if (!sourceEcheance.HasValue) return null;

            DateTime sourceDebut = sourcePrs.DateDebut != default(DateTime) ? sourcePrs.DateDebut : sourcePrs.DateCreation;
            DateTime targetDebut = targetPrs.DateDebut != default(DateTime) ? targetPrs.DateDebut : targetPrs.DateCreation;

            var ecartJours = (sourceDebut - sourceEcheance.Value).Days; // CORRECTION : source MOINS échéance
            return targetDebut.AddDays(-ecartJours); // SOUSTRACTION pour maintenir le même écart
        }
    }
}