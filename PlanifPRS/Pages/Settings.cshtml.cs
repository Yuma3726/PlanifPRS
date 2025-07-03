using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlanifPRS.Pages
{
    public class SettingsModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public SettingsModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public List<PrsFamille> Familles { get; set; } = new List<PrsFamille>();

        [BindProperty]
        public PrsFamille NewFamille { get; set; } = new PrsFamille();

        public async Task<IActionResult> OnGetAsync()
        {
            // Vérification des droits d'accès (admin uniquement)
            var login = User.Identity?.Name?.Split('\\').LastOrDefault();
            var user = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.LoginWindows == login);

            if (user == null || user.Droits?.ToLower() != "admin")
            {
                TempData["ErrorMessage"] = "⚠️ Accès refusé. Seuls les administrateurs peuvent accéder aux paramètres.";
                return RedirectToPage("/Index");
            }

            try
            {
                Familles = await _context.PrsFamilles
                    .OrderBy(f => f.Libelle)
                    .ToListAsync();

                TempData["InfoMessage"] = $"✅ {Familles.Count} famille(s) chargée(s) - Utilisateur: {login}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Erreur lors du chargement des familles: {ex.Message}";
                Familles = new List<PrsFamille>();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(NewFamille?.Libelle))
                {
                    // Vérification des doublons
                    var existingFamille = await _context.PrsFamilles
                        .FirstOrDefaultAsync(f => f.Libelle.ToLower() == NewFamille.Libelle.ToLower());

                    if (existingFamille != null)
                    {
                        TempData["ErrorMessage"] = $"⚠️ Une famille avec le nom '{NewFamille.Libelle}' existe déjà.";
                        return RedirectToPage();
                    }

                    // Valeur par défaut pour la couleur si vide
                    if (string.IsNullOrWhiteSpace(NewFamille.CouleurHex))
                    {
                        NewFamille.CouleurHex = "#3498db";
                    }

                    _context.PrsFamilles.Add(NewFamille);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"✅ Famille '{NewFamille.Libelle}' ajoutée avec succès!";
                }
                else
                {
                    TempData["ErrorMessage"] = "⚠️ Le nom de la famille est obligatoire.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Erreur lors de l'ajout: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            try
            {
                var updated = Familles?.FirstOrDefault();
                if (updated != null && updated.Id > 0)
                {
                    var existing = await _context.PrsFamilles.FindAsync(updated.Id);
                    if (existing != null)
                    {
                        // Vérification des doublons (sauf pour l'enregistrement courant)
                        var duplicateFamille = await _context.PrsFamilles
                            .FirstOrDefaultAsync(f => f.Libelle.ToLower() == updated.Libelle.ToLower()
                                               && f.Id != updated.Id);

                        if (duplicateFamille != null)
                        {
                            TempData["ErrorMessage"] = $"⚠️ Une autre famille avec le nom '{updated.Libelle}' existe déjà.";
                            return RedirectToPage();
                        }

                        var ancienLibelle = existing.Libelle;
                        existing.Libelle = updated.Libelle?.Trim();
                        existing.CouleurHex = updated.CouleurHex;

                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = $"✅ Famille '{ancienLibelle}' mise à jour vers '{existing.Libelle}'!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "❌ Famille introuvable.";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "⚠️ Données invalides pour la mise à jour.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Erreur lors de la mise à jour: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var famille = await _context.PrsFamilles.FindAsync(id);
                if (famille != null)
                {
                    // Vérifier si la famille est utilisée dans des PRS
                    var prsUtilisant = await _context.Prs
                        .Where(p => p.FamilleId == id)
                        .CountAsync();

                    if (prsUtilisant > 0)
                    {
                        TempData["ErrorMessage"] = $"⚠️ Impossible de supprimer la famille '{famille.Libelle}'. Elle est utilisée dans {prsUtilisant} PRS.";
                        return RedirectToPage();
                    }

                    var nomFamille = famille.Libelle;
                    _context.PrsFamilles.Remove(famille);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"✅ Famille '{nomFamille}' supprimée avec succès!";
                }
                else
                {
                    TempData["ErrorMessage"] = "❌ Famille introuvable.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Erreur lors de la suppression: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}