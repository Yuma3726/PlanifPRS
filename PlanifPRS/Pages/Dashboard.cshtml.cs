using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlanifPRS.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public DashboardModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        // Métriques principales
        public int TotalPrs { get; set; }
        public int PrsEnAttente { get; set; }
        public int PrsValidees { get; set; }
        public int PrsAnnulees { get; set; }

        // Collections pour alertes et activité
        public List<Models.Prs> DerniersPrs { get; set; } = new List<Models.Prs>();
        public List<Models.Prs> PrsEnRetard { get; set; } = new List<Models.Prs>();
        public List<Models.Prs> PrsProches { get; set; } = new List<Models.Prs>();

        // Nouvelles métriques (simplifiées)
        public Dictionary<string, int> PrsParFamille { get; set; } = new Dictionary<string, int>();
        public double TauxReussite { get; set; }
        public TimeSpan DureeMoyenne { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Vérification de l'authentification
            var login = User.Identity?.Name?.Split('\\').LastOrDefault();
            if (string.IsNullOrEmpty(login))
            {
                TempData["ErrorMessage"] = "⚠️ Session expirée. Veuillez vous reconnecter.";
                return RedirectToPage("/Index");
            }

            var user = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.LoginWindows == login);

            if (user == null)
            {
                TempData["ErrorMessage"] = "⚠️ Utilisateur non trouvé dans le système.";
                return RedirectToPage("/Index");
            }

            try
            {
                var now = DateTime.Now;
                var prochainSeuil = now.AddDays(7);
                var debutMois = new DateTime(now.Year, now.Month, 1);

                // Métriques de base
                TotalPrs = await _context.Prs.CountAsync();
                PrsEnAttente = await _context.Prs.CountAsync(p => p.Statut == "En attente");
                PrsValidees = await _context.Prs.CountAsync(p => p.Statut == "Validé");
                PrsAnnulees = await _context.Prs.CountAsync(p => p.Statut == "Annulé");

                // PRS récentes (dernières 10)
                DerniersPrs = await _context.Prs
                    .OrderByDescending(p => p.DateCreation)
                    .Take(10)
                    .ToListAsync();

                // PRS en retard
                PrsEnRetard = await _context.Prs
                    .Where(p => p.DateFin < now && p.Statut != "Validé" && p.Statut != "Annulé")
                    .OrderBy(p => p.DateFin)
                    .ToListAsync();

                // PRS à venir dans les 7 jours
                PrsProches = await _context.Prs
                    .Where(p => p.DateDebut >= now && p.DateDebut <= prochainSeuil)
                    .OrderBy(p => p.DateDebut)
                    .ToListAsync();

                // ✅ CORRECTION : Statistiques par famille simplifiées
                var famillesQuery = await _context.Prs
                    .Include(p => p.Famille)
                    .Where(p => p.DateCreation >= debutMois)
                    .Select(p => p.Famille != null ? p.Famille.Libelle : "Non définie")
                    .ToListAsync();

                PrsParFamille = famillesQuery
                    .GroupBy(f => f ?? "Non définie")
                    .ToDictionary(g => g.Key, g => g.Count());

                // Taux de réussite (PRS validées / PRS terminées)
                var prsTerminees = await _context.Prs
                    .CountAsync(p => p.Statut == "Validé" || p.Statut == "Annulé");

                TauxReussite = prsTerminees > 0
                    ? Math.Round((double)PrsValidees / prsTerminees * 100, 1)
                    : 0;

                // ✅ CORRECTION : Durée moyenne simplifiée
                var prsValidees = await _context.Prs
                    .Where(p => p.Statut == "Validé")
                    .Select(p => new { p.DateDebut, p.DateFin })
                    .ToListAsync();

                if (prsValidees.Any())
                {
                    var durees = prsValidees
                        .Select(p => (p.DateFin - p.DateDebut).TotalMinutes)
                        .Where(d => d > 0);

                    if (durees.Any())
                    {
                        var dureeMoyenneMinutes = durees.Average();
                        DureeMoyenne = TimeSpan.FromMinutes(dureeMoyenneMinutes);
                    }
                }

                // Messages de succès avec statistiques
                var messageStats = $"✅ Dashboard chargé - {TotalPrs} PRS total - {PrsEnRetard.Count} en retard - {PrsProches.Count} à venir - Taux réussite: {TauxReussite}%";
                TempData["InfoMessage"] = messageStats;

                // Log détaillé pour monitoring
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Dashboard accédé par {login}:");
                Console.WriteLine($"  - Total PRS: {TotalPrs}");
                Console.WriteLine($"  - En attente: {PrsEnAttente}");
                Console.WriteLine($"  - Validées: {PrsValidees}");
                Console.WriteLine($"  - En retard: {PrsEnRetard.Count}");
                Console.WriteLine($"  - À venir (7j): {PrsProches.Count}");
                Console.WriteLine($"  - Taux réussite: {TauxReussite}%");
                Console.WriteLine($"  - Durée moyenne: {DureeMoyenne:hh\\:mm}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERREUR Dashboard pour {login}: {ex}");
                TempData["ErrorMessage"] = $"❌ Erreur lors du chargement du dashboard: {ex.Message}";

                // Valeurs par défaut en cas d'erreur
                TotalPrs = 0;
                PrsEnAttente = 0;
                PrsValidees = 0;
                DerniersPrs = new List<Models.Prs>();
                PrsEnRetard = new List<Models.Prs>();
                PrsProches = new List<Models.Prs>();
            }

            return Page();
        }

        // Action AJAX pour rafraîchir les métriques
        public async Task<IActionResult> OnGetRefreshMetricsAsync()
        {
            try
            {
                var now = DateTime.Now;

                var metrics = new
                {
                    totalPrs = await _context.Prs.CountAsync(),
                    prsEnAttente = await _context.Prs.CountAsync(p => p.Statut == "En attente"),
                    prsValidees = await _context.Prs.CountAsync(p => p.Statut == "Validé"),
                    prsEnRetard = await _context.Prs.CountAsync(p => p.DateFin < now && p.Statut != "Validé" && p.Statut != "Annulé"),
                    lastUpdate = now.ToString("HH:mm:ss")
                };

                return new JsonResult(metrics);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }
        }
    }
}