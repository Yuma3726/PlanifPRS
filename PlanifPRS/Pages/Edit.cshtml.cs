using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlanifPRS.Pages
{
    public class EditModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public EditModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        // Support pour les deux structures de vue
        public SelectList LigneList { get; set; }
        public SelectList FamillesSelectList { get; set; }
        public IList<PrsFamille> Familles { get; set; }

        [TempData]
        public string Flash { get; set; }

        // Propriété pour indiquer si l'utilisateur peut modifier cette PRS
        public bool CanEditPrs { get; private set; }

        // Propriété pour indiquer si l'utilisateur actuel est admin ou validateur
        public bool IsAdminOrValidateur => HasRequiredRole();

        // Propriété pour obtenir le login de l'utilisateur actuel
        public string CurrentUserLogin => GetCurrentUserLogin();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Accès à Edit avec id={id} | Utilisateur: {CurrentUserLogin}");

            Prs = await _context.Prs.FindAsync(id);

            if (Prs == null)
            {
                return NotFound();
            }

            // Vérifier si l'utilisateur a les droits de modification
            CanEditPrs = CheckEditPermissions(Prs);

            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Utilisateur {CurrentUserLogin} accède à la PRS {id} en mode {(CanEditPrs ? "modification" : "lecture seule")}");

            // Chargement avec fallback SQL en cas d'erreur Entity Framework
            await ChargerFamillesAsync();
            await ChargerLignesAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Récupérer la PRS d'origine pour vérification des droits
            var originalPrs = await _context.Prs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == Prs.Id);

            // Vérifier si la PRS existe
            if (originalPrs == null)
            {
                return NotFound();
            }

            // Vérifier les droits de modification
            CanEditPrs = CheckEditPermissions(originalPrs);

            if (!CanEditPrs)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Tentative non autorisée de modification de la PRS {Prs.Id} par {CurrentUserLogin}");
                ModelState.AddModelError(string.Empty, "Vous n'avez pas les droits nécessaires pour modifier cette PRS.");
                await ChargerFamillesAsync();
                await ChargerLignesAsync();
                return Page();
            }

            // Si l'utilisateur n'est pas admin/validateur et qu'on est en mode semaine
            if (!IsAdminOrValidateur && Request.Form.ContainsKey("weekMode") && Request.Form["weekMode"] == "true")
            {
                // Récupérer la semaine sélectionnée
                if (Request.Form.ContainsKey("selectedWeek") &&
                    DateTime.TryParse(Request.Form["selectedWeek"], out var weekStartDate))
                {
                    // Définir la période du lundi 00:00 au lundi suivant 00:00
                    var mondayStart = GetMondayOfWeek(weekStartDate);
                    var sundayEnd = mondayStart.AddDays(7); // Lundi suivant à 00:00:00

                    Prs.DateDebut = mondayStart;
                    Prs.DateFin = sundayEnd;
                }
            }

            // Validation des dates
            if (Prs.DateDebut >= Prs.DateFin)
            {
                ModelState.AddModelError(string.Empty, "La date de début doit être antérieure à la date de fin.");
            }

            if (!ModelState.IsValid)
            {
                // Recharger les listes en cas d'erreur
                await ChargerFamillesAsync();
                await ChargerLignesAsync();
                return Page();
            }

            try
            {
                var prsFromDb = await _context.Prs.FindAsync(Prs.Id);
                if (prsFromDb == null)
                    return NotFound();

                // Préserver certaines informations de l'original
                var dateCreation = prsFromDb.DateCreation;
                var createdByLogin = prsFromDb.CreatedByLogin;
                var couleurOriginal = prsFromDb.CouleurPRS;

                // Mise à jour des champs
                prsFromDb.Titre = CleanEmojis(Prs.Titre);
                prsFromDb.Equipement = Prs.Equipement;
                prsFromDb.ReferenceProduit = Prs.ReferenceProduit;
                prsFromDb.Quantite = Prs.Quantite;
                prsFromDb.BesoinOperateur = Prs.BesoinOperateur;
                prsFromDb.PresenceClient = Prs.PresenceClient;
                prsFromDb.DateDebut = Prs.DateDebut;
                prsFromDb.DateFin = Prs.DateFin;
                prsFromDb.Statut = Prs.Statut;
                prsFromDb.InfoDiverses = Prs.InfoDiverses;
                prsFromDb.FamilleId = Prs.FamilleId;
                prsFromDb.LigneId = Prs.LigneId;
                prsFromDb.DerniereModification = DateTime.Now;
                prsFromDb.DateCreation = dateCreation;
                prsFromDb.CreatedByLogin = createdByLogin;

                // Gestion de la couleur PRS
                if (!IsAdminOrValidateur)
                {
                    // Si pas admin/validateur, on conserve la couleur originale
                    prsFromDb.CouleurPRS = couleurOriginal;
                }
                else if (string.IsNullOrWhiteSpace(Prs.CouleurPRS))
                {
                    prsFromDb.CouleurPRS = null;
                }
                else
                {
                    prsFromDb.CouleurPRS = Prs.CouleurPRS;
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PRS {Prs.Id} modifiée avec succès par {CurrentUserLogin}");
                Flash = "PRS modifiée avec succès ✅";
                return RedirectToPage("/Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Prs.Any(e => e.Id == Prs.Id))
                {
                    return NotFound();
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Erreur de concurrence lors de la modification. Un autre utilisateur a peut-être modifié cette PRS.");
                    await ChargerFamillesAsync();
                    await ChargerLignesAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Erreur lors de la modification de la PRS {Prs.Id}: {ex.Message}");
                ModelState.AddModelError(string.Empty, $"Une erreur est survenue : {ex.Message}");
                await ChargerFamillesAsync();
                await ChargerLignesAsync();
                return Page();
            }
        }

        /// <summary>
        /// Obtient la date du lundi de la semaine contenant la date spécifiée
        /// </summary>
        private DateTime GetMondayOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date; // .Date pour avoir 00:00:00
        }

        /// <summary>
        /// Vérifie si l'utilisateur actuel a le droit de modifier cette PRS
        /// </summary>
        private bool CheckEditPermissions(Models.Prs prs)
        {
            // Admin ou validateur peut toujours modifier
            if (IsAdminOrValidateur)
            {
                return true;
            }

            // Le créateur de la PRS peut la modifier
            var currentLogin = GetCurrentUserLogin();
            if (!string.IsNullOrEmpty(currentLogin) &&
                !string.IsNullOrEmpty(prs.CreatedByLogin) &&
                currentLogin.Equals(prs.CreatedByLogin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Tous les autres utilisateurs ne peuvent pas modifier
            return false;
        }

        /// <summary>
        /// Obtient le login de l'utilisateur actuel au format approprié
        /// </summary>
        private string GetCurrentUserLogin()
        {
            var fullLogin = User.Identity?.Name;

            if (string.IsNullOrEmpty(fullLogin))
                return "Utilisateur inconnu";

            // Extraction du login depuis le format domain\username
            var loginParts = fullLogin.Split('\\');
            return loginParts.Length > 1 ? loginParts[1] : fullLogin;
        }

        /// <summary>
        /// Vérification des droits utilisateur (admin ou validateur)
        /// </summary>
        private bool HasRequiredRole()
        {
            try
            {
                // Nettoyer le login comme dans votre code Users
                var login = GetCurrentUserLogin();

                if (string.IsNullOrEmpty(login))
                {
                    return false;
                }

                // Chercher l'utilisateur dans la base
                var user = _context.Utilisateurs.FirstOrDefault(u => u.LoginWindows == login && !u.DateDeleted.HasValue);

                if (user == null)
                {
                    return false;
                }

                // Vérifier les droits requis (admin ou validateur)
                var droitsAutorises = new[] { "admin", "validateur" };
                var droitUser = user.Droits?.ToLower() ?? "";

                return droitsAutorises.Contains(droitUser);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Erreur vérification droits: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nettoie les emojis et caractères spéciaux d'une chaîne
        /// </summary>
        private string CleanEmojis(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Nettoyer les emojis
            string cleanedText = Regex.Replace(input, @"[\u00A0-\u9999\uD800-\uDFFF]", "", RegexOptions.Compiled);

            // Si le texte commence par des caractères communs avec des emojis comme "🏭 CMS" → "CMS"
            cleanedText = Regex.Replace(cleanedText, @"^\s*[^\w]*\s*", "");

            // Cas spécifiques connus
            cleanedText = cleanedText.Replace("👨‍🔧 Besoin opérateur", "Besoin opérateur");
            cleanedText = cleanedText.Replace("❌ Aucun", "Aucun");
            cleanedText = cleanedText.Replace("✅ Client présent", "Client présent");
            cleanedText = cleanedText.Replace("❌ Client absent", "Client absent");
            cleanedText = cleanedText.Replace("❓ Non spécifié", "Non spécifié");

            return cleanedText.Trim();
        }

        // Méthode pour charger les familles avec fallback SQL
        private async Task ChargerFamillesAsync()
        {
            try
            {
                // Tentative avec Entity Framework d'abord
                var familles = await _context.PrsFamilles
                    .Where(f => f != null && !string.IsNullOrEmpty(f.Libelle))
                    .OrderBy(f => f.Libelle)
                    .ToListAsync();

                FamillesSelectList = new SelectList(familles, "Id", "Libelle", Prs.FamilleId);
                Familles = familles; // Pour la nouvelle structure de vue

                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Familles chargées via EF (Edit): {familles.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Erreur EF familles, basculement SQL: {ex.Message}");

                // Fallback avec SQL brut
                await ChargerFamillesSQLAsync();
            }
        }

        // Méthode pour charger les lignes avec fallback SQL
        private async Task ChargerLignesAsync()
        {
            try
            {
                // Tentative avec Entity Framework d'abord
                var lignes = await _context.Lignes
                    .Where(l => l != null && l.Activation == true && !string.IsNullOrEmpty(l.Nom))
                    .OrderBy(l => l.Nom)
                    .ToListAsync();

                LigneList = new SelectList(lignes, "Id", "Nom", Prs.LigneId);

                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Lignes chargées via EF (Edit): {lignes.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Erreur EF lignes, basculement SQL: {ex.Message}");

                // Fallback avec SQL brut
                await ChargerLignesSQLAsync();
            }
        }

        // Fallback SQL pour les familles
        private async Task ChargerFamillesSQLAsync()
        {
            try
            {
                var famillesList = new List<PrsFamille>();
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Libelle, CouleurHex 
                    FROM [PlanifPRS].[dbo].[PRS_Famille] 
                    WHERE Libelle IS NOT NULL AND Libelle != ''
                    ORDER BY Libelle";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idObj = reader[0];
                    var libelleObj = reader[1];
                    var couleurObj = reader[2];

                    int id = 0;
                    if (idObj != null && idObj != DBNull.Value)
                    {
                        if (int.TryParse(idObj.ToString(), out int parsedId))
                        {
                            id = parsedId;
                        }
                    }

                    if (id > 0)
                    {
                        famillesList.Add(new PrsFamille
                        {
                            Id = id,
                            Libelle = libelleObj?.ToString() ?? "Sans nom",
                            CouleurHex = couleurObj?.ToString() ?? "#009dff"
                        });
                    }
                }

                FamillesSelectList = new SelectList(famillesList, "Id", "Libelle", Prs.FamilleId);
                Familles = famillesList;

                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Familles chargées via SQL (Edit): {famillesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Erreur SQL familles (Edit): {ex.Message}");
                FamillesSelectList = new SelectList(new List<object>(), "Id", "Libelle");
                Familles = new List<PrsFamille>();
            }
        }

        // Fallback SQL pour les lignes
        private async Task ChargerLignesSQLAsync()
        {
            try
            {
                var lignesList = new List<SelectListItem>();
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Nom 
                    FROM [PlanifPRS].[dbo].[Lignes] 
                    WHERE activation = 1 AND Nom IS NOT NULL AND Nom != ''
                    ORDER BY Nom";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var idObj = reader[0];
                    var nomObj = reader[1];

                    int id = 0;
                    if (idObj != null && idObj != DBNull.Value)
                    {
                        if (int.TryParse(idObj.ToString(), out int parsedId))
                        {
                            id = parsedId;
                        }
                    }

                    if (id > 0)
                    {
                        lignesList.Add(new SelectListItem
                        {
                            Value = id.ToString(),
                            Text = nomObj?.ToString() ?? "Sans nom",
                            Selected = Prs?.LigneId == id
                        });
                    }
                }

                LigneList = new SelectList(lignesList, "Value", "Text", Prs?.LigneId);

                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Lignes chargées via SQL (Edit): {lignesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Erreur SQL lignes (Edit): {ex.Message}");
                LigneList = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }
    }
}