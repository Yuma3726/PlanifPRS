using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlanifPRS.Data;
using PlanifPRS.Models;
using PlanifPRS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PlanifPRS.Pages.Prs
{
    public class CreateModel : PageModel
    {
        private readonly PlanifPrsDbContext _context;
        private readonly FileService _fileService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(PlanifPrsDbContext context, FileService fileService, ILogger<CreateModel> logger)
        {
            _context = context;
            _fileService = fileService;
            _logger = logger;
        }

        [BindProperty]
        public Models.Prs Prs { get; set; }

        [BindProperty]
        public List<IFormFile> UploadedFiles { get; set; }

        public SelectList LigneList { get; set; }

        public IList<PrsFamille> Familles { get; set; }

        [TempData]
        public string Flash { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

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
            _logger.LogInformation($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Début de OnPostAsync avec {UploadedFiles?.Count ?? 0} fichiers");

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
                _logger.LogInformation("Création d'une nouvelle PRS");

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
                _logger.LogInformation($"PRS créée avec succès. ID: {Prs.Id}, Titre: {Prs.Titre}");

                // Gérer l'upload des fichiers après la création de la PRS
                if (UploadedFiles != null && UploadedFiles.Any())
                {
                    _logger.LogInformation($"Traitement de {UploadedFiles.Count} fichiers uploadés");

                    // Debug des fichiers reçus
                    foreach (var file in UploadedFiles)
                    {
                        _logger.LogInformation($"Fichier reçu: {file.FileName}, Type: {file.ContentType}, Taille: {file.Length} bytes");
                    }

                    // Stocker les fichiers
                    var fileResults = await _fileService.SaveMultipleFilesAsync(
                        UploadedFiles,
                        Prs.Id.ToString(),
                        Prs.Titre ?? "PRS"
                    );

                    int successCount = 0;
                    int errorCount = 0;

                    // Créer les enregistrements de fichiers dans la base de données
                    foreach (var (Success, FilePath, ErrorMsg) in fileResults)
                    {
                        if (Success && !string.IsNullOrEmpty(FilePath))
                        {
                            var file = UploadedFiles[fileResults.IndexOf((Success, FilePath, ErrorMsg))];
                            var prsFichier = new PrsFichier
                            {
                                PrsId = Prs.Id,
                                NomOriginal = file.FileName,
                                CheminFichier = FilePath,
                                TypeMime = file.ContentType,
                                Taille = file.Length,
                                DateUpload = DateTime.Now,
                                UploadParLogin = GetCurrentUserLogin()
                            };

                            _logger.LogInformation($"Ajout du fichier à la BDD: {prsFichier.NomOriginal}, Chemin: {prsFichier.CheminFichier}");
                            _context.PrsFichiers.Add(prsFichier);
                            successCount++;
                        }
                        else
                        {
                            _logger.LogError($"Erreur lors de l'upload: {ErrorMsg}");
                            errorCount++;
                            ModelState.AddModelError(string.Empty, ErrorMsg);
                        }
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Fin de traitement des fichiers: {successCount} succès, {errorCount} erreurs");

                    if (successCount > 0)
                    {
                        Flash += $" {successCount} fichier(s) téléchargé(s) avec succès.";
                    }

                    if (errorCount > 0)
                    {
                        ErrorMessage = $"{errorCount} fichier(s) n'ont pas pu être téléchargés. Vérifiez les erreurs.";
                    }
                }
                else
                {
                    _logger.LogInformation("Aucun fichier à uploader");
                }

                Flash = "PRS ajoutée avec succès ✅";

                return RedirectToPage("./Create");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur lors de l'ajout de la PRS: {ex.Message}";
                ModelState.AddModelError(string.Empty, "Erreur lors de l'ajout de la PRS.");
                _logger.LogError($"Erreur lors de la création de la PRS: {ex.Message}", ex);
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
                _logger.LogError($"Erreur vérification droits: {ex.Message}");
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
                _logger.LogInformation($"Familles chargées: {famillesList.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur ChargerFamilles: {ex.Message}");
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
                _logger.LogInformation($"Lignes chargées: {lignesList.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur ChargerLignes: {ex.Message}");
                LigneList = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }
    }
}