using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanifPRS.Pages
{
    public class MilestonesModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public MilestonesModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        public List<PrsJalon> Jalons { get; set; } = new List<PrsJalon>();

        public async Task<IActionResult> OnGetAsync()
        {
            // Vérification des droits d'accès
            var login = User.Identity?.Name?.Split('\\').LastOrDefault();
            var user = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.LoginWindows == login);

            if (user == null)
            {
                TempData["ErrorMessage"] = "⚠️ Utilisateur non trouvé dans le système.";
                return RedirectToPage("/Index");
            }

            // Accès selon les droits
            var droitsAutorises = new[] { "admin", "cdp", "validateur", "process" };
            if (!droitsAutorises.Contains(user.Droits?.ToLower()))
            {
                TempData["ErrorMessage"] = "⚠️ Accès refusé. Droits insuffisants pour consulter les jalons.";
                return RedirectToPage("/Index");
            }

            try
            {
                Jalons = await _context.PrsJalons
                    .Include(j => j.Prs)
                    .Include(j => j.JalonUtilisateurs)
                        .ThenInclude(ju => ju.Utilisateur)
                    .OrderBy(j => j.DatePrevue)
                    .ThenBy(j => j.EstValide)
                    .ThenBy(j => j.Prs.Titre)
                    .ToListAsync();

                // Statistiques pour les logs
                var totalJalons = Jalons.Count;
                var jalonsValides = Jalons.Count(j => j.EstValide);
                var jalonsEnRetard = Jalons.Count(j => j.DatePrevue.HasValue
                    && j.DatePrevue.Value.Date < DateTime.Today
                    && !j.EstValide);

                TempData["InfoMessage"] = $"✅ {totalJalons} jalon(s) chargé(s) - {jalonsValides} validé(s) - {jalonsEnRetard} en retard - Utilisateur: {login}";

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Jalons chargés par {login}: {totalJalons} total, {jalonsValides} validés, {jalonsEnRetard} en retard");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERREUR chargement jalons par {login}: {ex}");
                TempData["ErrorMessage"] = $"❌ Erreur lors du chargement des jalons: {ex.Message}";
                Jalons = new List<PrsJalon>();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostToggleValidationAsync(int jalonId)
        {
            try
            {
                var jalon = await _context.PrsJalons.FindAsync(jalonId);
                if (jalon != null)
                {
                    jalon.EstValide = !jalon.EstValide;
                    await _context.SaveChangesAsync();

                    var status = jalon.EstValide ? "validé" : "non validé";
                    TempData["SuccessMessage"] = $"✅ Jalon '{jalon.NomJalon}' marqué comme {status}!";
                }
                else
                {
                    TempData["ErrorMessage"] = "❌ Jalon introuvable.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Erreur lors de la mise à jour: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}