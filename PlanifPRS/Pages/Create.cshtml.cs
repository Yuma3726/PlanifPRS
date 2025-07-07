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

namespace PlanifPRS.Pages.Prs
{
    public class CreateModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;

        public CreateModel(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        public SelectList LigneList { get; set; }

        public IList<PrsFamille> Familles { get; set; }

        [TempData]
        public string Flash { get; set; }

        // Propriété pour vérifier les droits utilisateur
        public bool IsAdminOrValidateur => HasRequiredRole();

        // Propriété pour obtenir le login de l'utilisateur actuel
        public string CurrentUserLogin => GetCurrentUserLogin();

        public void OnGet()
        {
            ChargerFamilles();
            ChargerLignes();

            var now = DateTime.Now;
            var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            // Valeurs par défaut
            Prs = new Models.Prs
            {
                // Le statut est défini automatiquement selon les droits de l'utilisateur
                Statut = IsAdminOrValidateur ? "Validé" : "En attente",
                DateDebut = rounded,
                DateFin = rounded.AddHours(1)
            };

            // Écrase les valeurs par défaut si une date est passée dans l'URL
            if (Request.Query.ContainsKey("start"))
            {
                if (DateTime.TryParse(Request.Query["start"], out var parsedStart))
                {
                    Prs.DateDebut = parsedStart;
                    Prs.DateFin = parsedStart.AddHours(1);
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ChargerFamilles();
            ChargerLignes();

            if (Prs.DateDebut >= Prs.DateFin)
            {
                ModelState.AddModelError(string.Empty, "La date de début doit être antérieure à la date de fin.");
            }

            if (Prs.LigneId == 0)
            {
                ModelState.AddModelError("Prs.LigneId", "La sélection d'une ligne est obligatoire.");
            }

            // Définir automatiquement le statut en fonction des droits de l'utilisateur
            Prs.Statut = IsAdminOrValidateur ? "Validé" : "En attente";

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                Prs.DateCreation = DateTime.Now;
                Prs.DerniereModification = DateTime.Now;

                // Définir le login de l'utilisateur qui crée la PRS
                Prs.CreatedByLogin = GetCurrentUserLogin();

                // Nettoyer les emojis et caractères spéciaux des champs textuels
                Prs.Titre = CleanEmojis(Prs.Titre);
                Prs.Equipement = CleanEmojis(Prs.Equipement);
                Prs.BesoinOperateur = CleanEmojis(Prs.BesoinOperateur);
                Prs.PresenceClient = CleanEmojis(Prs.PresenceClient);

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

                // Gestion de la couleur PRS
                if (!IsAdminOrValidateur || string.IsNullOrWhiteSpace(Prs.CouleurPRS))
                {
                    Prs.CouleurPRS = null; // Seuls les admin/validateurs peuvent définir une couleur
                }

                _context.Prs.Add(Prs);
                await _context.SaveChangesAsync();

                Flash = "PRS ajoutée avec succès ✅";

                return RedirectToPage("./Create");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Erreur lors de l'ajout de la PRS.");
                Console.WriteLine(">>>> ERREUR : " + ex.Message);
                return Page();
            }
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
        /// Obtient la date du lundi de la semaine contenant la date spécifiée
        /// </summary>
        private DateTime GetMondayOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date; // .Date pour avoir 00:00:00
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
                Console.WriteLine($"❌ Erreur vérification droits: {ex.Message}");
                return false;
            }
        }

        private void ChargerFamilles()
        {
            try
            {
                var famillesList = new List<PrsFamille>();
                var connection = _context.Database.GetDbConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Libelle, CouleurHex 
                    FROM [PlanifPRS].[dbo].[PRS_Famille] 
                    WHERE Libelle IS NOT NULL AND Libelle != ''
                    ORDER BY Libelle";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Accès par index au lieu du nom pour éviter les erreurs de type
                    var idObj = reader[0]; // Première colonne = Id
                    var libelleObj = reader[1]; // Deuxième colonne = Libelle
                    var couleurObj = reader[2]; // Troisième colonne = CouleurHex

                    // Conversion sécurisée de l'ID
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

                Familles = famillesList;
                Console.WriteLine($"✅ Familles chargées: {famillesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur ChargerFamilles: {ex.Message}");
                Familles = new List<PrsFamille>();
            }
        }

        private void ChargerLignes()
        {
            try
            {
                var lignesList = new List<SelectListItem>();
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Nom 
                    FROM [PlanifPRS].[dbo].[Lignes] 
                    WHERE activation = 1 AND Nom IS NOT NULL AND Nom != ''
                    ORDER BY Nom";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Accès par index au lieu du nom pour éviter les erreurs de type
                    var idObj = reader[0]; // Première colonne = Id
                    var nomObj = reader[1]; // Deuxième colonne = Nom

                    // Conversion sécurisée de l'ID
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
                            Text = nomObj?.ToString() ?? "Sans nom"
                        });
                    }
                }

                LigneList = new SelectList(lignesList, "Value", "Text");
                Console.WriteLine($"✅ Lignes chargées: {lignesList.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur ChargerLignes: {ex.Message}");
                LigneList = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }
    }
}